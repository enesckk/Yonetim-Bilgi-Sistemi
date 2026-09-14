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

        services.AddScoped<AuditingSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
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

        // Data Protection anahtarları diskte — restart’ta TCKN’ler okunabilir kalsın.
        // Production (Windows): DPAPI ile anahtar dosyaları şifrelenir.
        var configuredKeysPath = configuration["Security:DataProtectionKeysPath"];
        if (!environment.IsDevelopment() && OperatingSystem.IsLinux() &&
            (string.IsNullOrWhiteSpace(configuredKeysPath) || !Path.IsPathFullyQualified(configuredKeysPath)))
            throw new InvalidOperationException("Security:DataProtectionKeysPath Production Linux ortamında mutlak ve kalıcı bir yol olmalıdır.");

        var dpKeys = string.IsNullOrWhiteSpace(configuredKeysPath)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "dp-keys")
            : Path.GetFullPath(configuredKeysPath);
        Directory.CreateDirectory(dpKeys);
        var dpBuilder = services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dpKeys))
            .SetApplicationName("PersonelYonetim");

        var protectKeys = configuration.GetValue("Security:ProtectDataProtectionKeys", !environment.IsDevelopment());
        if (!environment.IsDevelopment() && OperatingSystem.IsLinux() && !protectKeys)
            throw new InvalidOperationException("Production Linux ortamında Data Protection anahtar koruması kapatılamaz.");
        if (protectKeys && OperatingSystem.IsWindows())
        {
            // IIS app pool hesabı için LocalMachine önerilir; aksi halde CurrentUser.
            var useMachineKey = configuration.GetValue("Security:ProtectKeysWithMachineKey", true);
            dpBuilder.ProtectKeysWithDpapi(protectToLocalMachine: useMachineKey);
        }
        else if (protectKeys && !environment.IsDevelopment())
        {
            var encodedCertificate = configuration["Security:DataProtectionCertificateBase64"];
            var certificatePassword = configuration["Security:DataProtectionCertificatePassword"];
            if (string.IsNullOrWhiteSpace(encodedCertificate) || string.IsNullOrWhiteSpace(certificatePassword))
                throw new InvalidOperationException("Production Linux ortamında Data Protection sertifikası ve parolası zorunludur.");

            var certificate = X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(encodedCertificate),
                certificatePassword,
                X509KeyStorageFlags.EphemeralKeySet);
            if (!certificate.HasPrivateKey)
                throw new InvalidOperationException("Data Protection sertifikasının özel anahtarı yok.");
            dpBuilder.ProtectKeysWithCertificate(certificate);
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
        if (!environment.IsDevelopment() && OperatingSystem.IsLinux() &&
            (string.IsNullOrWhiteSpace(uploadsPath) || !Path.IsPathFullyQualified(uploadsPath)))
            throw new InvalidOperationException("FileStorage:RootPath Production Linux ortamında mutlak ve kalıcı bir yol olmalıdır.");

        services.Configure<Files.FileStorageOptions>(
            configuration.GetSection(Files.FileStorageOptions.SectionName));
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
