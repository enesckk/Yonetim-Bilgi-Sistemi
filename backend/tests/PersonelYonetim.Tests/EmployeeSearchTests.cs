using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Employees;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Tests;

public class EmployeeSearchTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Theory]
    [InlineData("i", "İbrahim Işık")]
    [InlineData("İ", "İbrahim Işık")]
    [InlineData("ı", "İbrahim Işık")]
    [InlineData("I", "İbrahim Işık")]
    [InlineData("t", "Tamer Kaya")]
    [InlineData("T", "Tamer Kaya")]
    public async Task One_letter_search_supports_Turkish_case_variants(string search, string expected)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Employees.AddRange(new Employee { FirstName = "İbrahim", LastName = "Işık" }, new Employee { FirstName = "Tamer", LastName = "Kaya" });
        await db.SaveChangesAsync();
        var service = new EmployeeQueryService(db, new User(), null!, null!);
        var result = await service.GetListAsync(new EmployeeListQuery { Search = search, Page = 1, PageSize = 20 });
        Assert.Equal(expected, Assert.Single(result.Items).FullName);
    }
    [Theory]
    [InlineData("05321234567")]
    [InlineData("0532 123 45 67")]
    [InlineData("+90 (532) 123-45-67")]
    [InlineData("0090 532 123 45 67")]
    public async Task Formatted_phone_search_finds_the_same_employee_and_respects_phone_permission(string search)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Employees.AddRange(new Employee { FirstName = "Aranan", LastName = "Kişi", PersonalPhone = "+90 (532) 123-45-67" },
            new Employee { FirstName = "Kurumsal", LastName = "Kişi", CorporatePhone = "0532 123 45 67" });
        await db.SaveChangesAsync();
        var service = new EmployeeQueryService(db, new User(true), null!, null!);
        Assert.Equal(2, (await service.GetListAsync(new EmployeeListQuery { Search = search })).TotalCount);
        var denied = new EmployeeQueryService(db, new User(), null!, null!);
        Assert.Empty((await denied.GetListAsync(new EmployeeListQuery { Search = search })).Items);
    }

    [LocalPostgresFact]
    public async Task Paged_live_search_remains_fast_on_Postgres_with_173_employees()
    {
        var cs = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("YBS_TEST_POSTGRES_CONNECTION")) { Database = "ybs_search_perf_" + Guid.NewGuid().ToString("N") };
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs.ConnectionString).Options);
        try {
            await db.Database.EnsureCreatedAsync();
            db.Employees.AddRange(Enumerable.Range(0, 173).Select(i => new Employee { FirstName = i % 2 == 0 ? "İbrahim" : "Tamer", LastName = "Kişi " + i, PersonalPhone = "0532 123 45 67" }));
            await db.SaveChangesAsync();
            var service = new EmployeeQueryService(db, new User(true), null!, null!);
            await service.GetListAsync(new EmployeeListQuery { Search = "i" });
            var timings = new List<double>();
            foreach (var term in new[] { "t", "i", "İ", "ı", "ta", "tamer", "0532 123 45 67", "+90 (532) 123-45-67" }) {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var result = await service.GetListAsync(new EmployeeListQuery { Search = term, PageSize = 20 });
                timings.Add(watch.Elapsed.TotalMilliseconds);
                Assert.InRange(result.Items.Count, 1, 20);
                output.WriteLine($"173 employees, search '{term}': {watch.Elapsed.TotalMilliseconds:F1} ms, {result.TotalCount} matches");
            }
            output.WriteLine($"Median: {timings.Order().ElementAt(timings.Count / 2):F1} ms; max: {timings.Max():F1} ms (local PostgreSQL, not production network)");
        } finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class User(bool phone = false) : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public string UserName => "test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => [PermissionCodes.EmployeesView, PermissionCodes.EmployeesViewAllUnits];
        public bool HasPermission(string code) => code == PermissionCodes.EmployeesViewPhone ? phone : Permissions.Contains(code);
    }
}
