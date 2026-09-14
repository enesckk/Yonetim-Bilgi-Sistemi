using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Api;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        if (string.Equals(Environment.GetEnvironmentVariable("Database__Provider"), "Postgres", StringComparison.OrdinalIgnoreCase))
            builder.UseNpgsql("Host=localhost;Database=personelyonetim_design;Username=postgres;Password=design_only", pg =>
                pg.MigrationsAssembly("PersonelYonetim.PostgresMigrations"));
        else
            builder.UseSqlServer("Server=localhost;Database=personelyonetim_design;User Id=sa;Password=design_only;TrustServerCertificate=True", sql =>
                sql.MigrationsAssembly("PersonelYonetim.Infrastructure"));
        var options = builder.Options;
        return new AppDbContext(options);
    }
}
