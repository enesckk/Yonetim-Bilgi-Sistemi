using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Employees;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Tests;

public class EmployeeSearchTests
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
    private sealed class User : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public string UserName => "test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => [PermissionCodes.EmployeesView, PermissionCodes.EmployeesViewAllUnits];
        public bool HasPermission(string code) => Permissions.Contains(code);
    }
}
