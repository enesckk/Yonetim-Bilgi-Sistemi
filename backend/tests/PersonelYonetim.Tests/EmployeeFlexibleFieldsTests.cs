using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Employees;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Persistence.Seed;

namespace PersonelYonetim.Tests;

public class EmployeeFlexibleFieldsTests
{
    [Fact]
    public async Task Main_units_are_added_once_without_moving_facilities_or_personnel()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var directorate = new OrganizationUnit { Code = "KSSIM", Name = "Müdürlük", Type = OrganizationUnitType.Directorate };
        var site = new OrganizationUnit { Code = "BILIM", Name = "Bilim Şehitkamil", Type = OrganizationUnitType.Facility, ParentId = directorate.Id };
        var gezi = new OrganizationUnit { Code = "GEZI", Name = "Kültürel Geziler", Type = OrganizationUnitType.MainUnit, ParentId = directorate.Id };
        var employee = new Employee { FirstName = "Test", LastName = "Personel", UnitId = gezi.Id, FacilityId = site.Id };
        db.AddRange(directorate, site, gezi, employee); await db.SaveChangesAsync();
        await EmployeeMainUnitsUpdate.SeedAsync(db); await EmployeeMainUnitsUpdate.SeedAsync(db);
        Assert.Equal(7, await db.OrganizationUnits.CountAsync(x => x.Type == OrganizationUnitType.MainUnit && x.ParentId == directorate.Id));
        Assert.Equal(OrganizationUnitType.Facility, site.Type); Assert.Equal(directorate.Id, site.ParentId);
        Assert.Equal(gezi.Id, employee.UnitId); Assert.Equal(site.Id, employee.FacilityId);
    }

    [Fact]
    public async Task Editing_persists_custom_title_and_duty_then_reuses_them_instead_of_duplicates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var employee = new Employee { FirstName = "Test", LastName = "Personel" };
        db.Employees.Add(employee); await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new UpdateEmployeeHandler(db, new User(), null!, null!, cache);
        var command = new UpdateEmployeeCommand { Id = employee.Id, FirstName = "Test", LastName = "Personel", Status = EmployeeStatus.Active, JobTitleName = "  Bilim  Koordinatörü ", PrimaryJobDutyName = "Atölye Planlama" };
        await handler.Handle(command, default);
        var title = await db.JobTitles.SingleAsync(); var duty = await db.JobDuties.SingleAsync();
        Assert.Equal("Bilim Koordinatörü", title.Name); Assert.Equal(title.Id, employee.JobTitleId);
        Assert.Equal(duty.Id, (await db.EmployeeAssignments.SingleAsync(x => x.IsPrimary)).JobDutyId);
        Assert.Equal(DutyCategory.Other, duty.Category);
        command.JobTitleId = null; command.PrimaryJobDutyId = null;
        command.JobTitleName = "bilim koordinatörü"; command.PrimaryJobDutyName = "atölye planlama";
        await handler.Handle(command, default);
        Assert.Single(await db.JobTitles.ToListAsync()); Assert.Single(await db.JobDuties.ToListAsync());
        Assert.Single(await db.EmployeeAssignments.ToListAsync());
        var options = await new GetEmployeeFormOptionsHandler(db, cache, new User()).Handle(new(), default);
        Assert.Contains(options.JobTitles, x => x.Id == title.Id);
        Assert.Contains(options.Duties, x => x.Id == duty.Id);
    }

    [Fact]
    public void Validators_reject_conflicting_selection_and_text_and_overlong_input()
    {
        var command = new CreateEmployeeRequest { FirstName = "Test", LastName = "Personel", JobTitleId = Guid.NewGuid(), JobTitleName = "Yeni unvan", PrimaryJobDutyName = new string('a', 201) };
        var result = new CreateEmployeeRequestValidator().Validate(command);
        Assert.Contains(result.Errors, x => x.PropertyName == "JobTitleName");
        Assert.Contains(result.Errors, x => x.PropertyName == "PrimaryJobDutyName");
    }

    private sealed class User : ICurrentUserService
    {
        public Guid? UserId => null; public string? UserName => "test"; public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => [PermissionCodes.EmployeesUpdate, PermissionCodes.EmployeesView, PermissionCodes.EmployeesViewAllUnits];
        public bool HasPermission(string permission) => Permissions.Contains(permission);
    }
}
