using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PersonelYonetim.Application.Common.Behaviors;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Application.Features.Import;
using PersonelYonetim.Application.Common.Security;
using PersonelYonetim.Infrastructure.Auth;
using PersonelYonetim.Infrastructure.Employees;
using PersonelYonetim.Infrastructure.Identity;
using PersonelYonetim.Infrastructure.Import;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' bulunamadı.");
        var usePostgres = string.Equals(configuration["Database:Provider"], "Postgres", StringComparison.OrdinalIgnoreCase);
        NpgsqlDataSource? postgresDataSource = null;
        if (usePostgres)
        {
            var pgConnection = new NpgsqlConnectionStringBuilder(connectionString);
            var encodedDatabaseCa = configuration["Supabase:DatabaseCaCertificateBase64"];
            var isSupabaseHost = pgConnection.Host?.EndsWith(".pooler.supabase.com", StringComparison.OrdinalIgnoreCase) == true ||
                                 pgConnection.Host?.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase) == true;
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            if (isSupabaseHost)
            {
                if (pgConnection.SslMode != SslMode.Require || string.IsNullOrWhiteSpace(encodedDatabaseCa))
                    throw new InvalidOperationException("Supabase PostgreSQL için SSL Mode=Require ve kök sertifika zorunludur; uygulama sertifika zincirini ve sunucu adını ayrıca doğrular.");

                var rootCertificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(encodedDatabaseCa));
                dataSourceBuilder.UseSslClientAuthenticationOptionsCallback(options =>
                {
                    options.RemoteCertificateValidationCallback = (_, certificate, presentedChain, _) =>
                        ValidateSupabaseServerCertificate(certificate, presentedChain, rootCertificate, pgConnection.Host!);
                });
            }
            else if (!string.IsNullOrWhiteSpace(encodedDatabaseCa))
                dataSourceBuilder.UseRootCertificate(X509CertificateLoader.LoadCertificate(
                    Convert.FromBase64String(encodedDatabaseCa)));
            postgresDataSource = dataSourceBuilder.Build();
            services.AddSingleton(postgresDataSource);
        }

        services.AddScoped<AuditingSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            if (usePostgres)
                options.UseNpgsql(postgresDataSource!, pg =>
                {
                    pg.MigrationsAssembly("PersonelYonetim.PostgresMigrations");
                    pg.EnableRetryOnFailure(maxRetryCount: 3);
                    pg.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });
            else
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    sql.EnableRetryOnFailure(maxRetryCount: 3);
                    sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });
            options.AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt ayarları bulunamadı.");

        ValidateJwtSigningKey(jwt.SigningKey, environment);

        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        // PostgreSQL'de key ring veritabanında; SQL Server'da kalıcı diskte tutulur.
        var configuredKeysPath = configuration["Security:DataProtectionKeysPath"];
        if (!usePostgres && !environment.IsDevelopment() && OperatingSystem.IsLinux() &&
            (string.IsNullOrWhiteSpace(configuredKeysPath) || !Path.IsPathFullyQualified(configuredKeysPath)))
            throw new InvalidOperationException("Security:DataProtectionKeysPath Production Linux ortamında mutlak ve kalıcı bir yol olmalıdır.");

        var dpKeys = string.IsNullOrWhiteSpace(configuredKeysPath)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "dp-keys")
            : Path.GetFullPath(configuredKeysPath);
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("PersonelYonetim");
        if (usePostgres)
            dpBuilder.PersistKeysToDbContext<AppDbContext>();
        else
        {
            Directory.CreateDirectory(dpKeys);
            dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(dpKeys));
        }

        var protectKeys = configuration.GetValue("Security:ProtectDataProtectionKeys", !environment.IsDevelopment());
        if (!environment.IsDevelopment() && OperatingSystem.IsLinux() && !protectKeys)
            throw new InvalidOperationException("Production Linux ortamında Data Protection anahtar koruması kapatılamaz.");
        var encodedCertificate = configuration["Security:DataProtectionCertificateBase64"];
        var certificatePassword = configuration["Security:DataProtectionCertificatePassword"];
        if (protectKeys && !environment.IsDevelopment() &&
            (!string.IsNullOrWhiteSpace(encodedCertificate) || !string.IsNullOrWhiteSpace(certificatePassword)))
        {
            if (string.IsNullOrWhiteSpace(encodedCertificate) || string.IsNullOrWhiteSpace(certificatePassword))
                throw new InvalidOperationException("Data Protection sertifikası ve parolası birlikte verilmelidir.");

            var certificate = X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(encodedCertificate),
                certificatePassword,
                OperatingSystem.IsMacOS() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet);
            if (!certificate.HasPrivateKey)
                throw new InvalidOperationException("Data Protection sertifikasının özel anahtarı yok.");
            dpBuilder.ProtectKeysWithCertificate(certificate);
        }
        else if (protectKeys && OperatingSystem.IsWindows())
        {
            // IIS app pool hesabı için LocalMachine önerilir; aksi halde CurrentUser.
            var useMachineKey = configuration.GetValue("Security:ProtectKeysWithMachineKey", true);
            dpBuilder.ProtectKeysWithDpapi(protectToLocalMachine: useMachineKey);
        }
        else if (protectKeys && !environment.IsDevelopment())
        {
            if (string.IsNullOrWhiteSpace(encodedCertificate) || string.IsNullOrWhiteSpace(certificatePassword))
                throw new InvalidOperationException("Production Linux ortamında Data Protection sertifikası ve parolası zorunludur.");
        }

        services.AddSingleton<INationalIdProtector, Security.NationalIdProtector>();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ISensitiveAccessLogger, Audit.SensitiveAccessLogger>();
        services.AddScoped<IEmployeeQueryService, EmployeeQueryService>();
        services.AddScoped<IEmployeeImportService, EmployeeImportService>();
        services.AddScoped<IUserNotificationService, Notifications.UserNotificationService>();
        services.AddScoped<INotificationScanService, Notifications.NotificationScanService>();
        services.AddHostedService<Notifications.NotificationScanHostedService>();
        services.AddScoped<IAppSettingsService, Settings.AppSettingsService>();

        // MediatR: request/validator Application'da, handler Infrastructure'da
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(PersonelYonetim.Application.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        var uploadsPath = configuration["FileStorage:RootPath"];
        if (!usePostgres && !environment.IsDevelopment() && OperatingSystem.IsLinux() &&
            (string.IsNullOrWhiteSpace(uploadsPath) || !Path.IsPathFullyQualified(uploadsPath)))
            throw new InvalidOperationException("FileStorage:RootPath Production Linux ortamında mutlak ve kalıcı bir yol olmalıdır.");

        services.Configure<Files.FileStorageOptions>(
            configuration.GetSection(Files.FileStorageOptions.SectionName));
        if (usePostgres)
        {
            services.AddOptions<Files.SupabaseStorageOptions>()
                .Bind(configuration.GetSection(Files.SupabaseStorageOptions.SectionName))
                .Validate(options => Uri.TryCreate(options.Url, UriKind.Absolute, out var url) &&
                    url.Scheme == Uri.UriSchemeHttps &&
                    url.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase) &&
                    options.SecretKey?.StartsWith("sb_secret_", StringComparison.Ordinal) == true &&
                    !string.IsNullOrWhiteSpace(options.Bucket),
                    "PostgreSQL için Supabase URL, yeni biçimde gizli anahtar ve özel bucket zorunludur.")
                .ValidateOnStart();
            services.AddHttpClient<IFileStorageService, Files.SupabaseFileStorageService>();
        }
        else
            services.AddScoped<IFileStorageService, Files.LocalFileStorageService>();

        services.AddHttpClient("Nominatim", client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "PersonelYonetim/1.0 (Sehitkamil Belediyesi; yerel-yonetim)");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
            client.Timeout = TimeSpan.FromSeconds(8);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        });
        services.AddScoped<IGeocodingService, Geo.NominatimGeocodingService>();

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = System.Security.Claims.ClaimTypes.Name
                };
            });

        var loginMax = configuration.GetValue("Security:LoginMaxAttemptsPerIp", 20);
        var loginWindow = configuration.GetValue("Security:LoginWindowMinutes", 15);
        services.AddSingleton(new LoginAttemptGate(
            loginMax,
            TimeSpan.FromMinutes(Math.Max(1, loginWindow))));

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    private static bool ValidateSupabaseServerCertificate(
        X509Certificate? certificate,
        X509Chain? presentedChain,
        X509Certificate2 rootCertificate,
        string host)
    {
        if (certificate is null)
            return false;

        using var serverCertificate = new X509Certificate2(certificate);
        using var verificationChain = new X509Chain();
        verificationChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        verificationChain.ChainPolicy.CustomTrustStore.Add(rootCertificate);
        verificationChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        if (presentedChain is not null)
        {
            foreach (var element in presentedChain.ChainElements.Cast<X509ChainElement>().Skip(1))
                verificationChain.ChainPolicy.ExtraStore.Add(element.Certificate);
        }

        return verificationChain.Build(serverCertificate) && serverCertificate.MatchesHostname(host);
    }

    private static void ValidateJwtSigningKey(string? signingKey, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey en az 32 karakter olmalıdır.");

        if (!environment.IsDevelopment() &&
            signingKey.StartsWith("LOCAL_DEV_ONLY", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey Production ortamında LOCAL_DEV_ONLY ile başlayamaz. Ortam değişkeni veya User Secrets kullanın.");
        }
    }
}
