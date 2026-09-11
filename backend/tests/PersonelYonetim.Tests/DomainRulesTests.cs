using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Domain.Events;
using PersonelYonetim.Domain.Settlements;
using PersonelYonetim.Domain.WorkTasks;

namespace PersonelYonetim.Tests;

public class EventAttendanceTests
{
    [Fact]
    public void Resolve_prefers_actual_including_zero()
    {
        Assert.Equal(0, EventAttendance.Resolve(0, 40, 100));
        Assert.Equal(842, EventAttendance.Resolve(842, 12, 900));
    }

    [Fact]
    public void Resolve_falls_back_to_settlement_then_expected()
    {
        Assert.Equal(40, EventAttendance.Resolve(null, 40, 100));
        Assert.Equal(100, EventAttendance.Resolve(null, 0, 100));
        Assert.Null(EventAttendance.Resolve(null, 0, null));
    }
}

public class WorkTaskLifecycleTests
{
    [Theory]
    [InlineData(WorkTaskStatus.Open, true, true)]
    [InlineData(WorkTaskStatus.InProgress, true, false)]
    [InlineData(WorkTaskStatus.WaitingApproval, true, false)]
    [InlineData(WorkTaskStatus.Cancelled, false, true)]
    [InlineData(WorkTaskStatus.Done, false, false)]
    [InlineData(WorkTaskStatus.Approved, false, false)]
    [InlineData(WorkTaskStatus.Rejected, false, false)]
    public void Cancel_and_delete_gates(WorkTaskStatus status, bool canCancel, bool canDelete)
    {
        Assert.Equal(canCancel, WorkTaskLifecycle.CanCancel(status));
        Assert.Equal(canDelete, WorkTaskLifecycle.CanDelete(status));
    }
}

public class SettlementCatalogTests
{
    [Fact]
    public void Normalizes_unknown_types_to_defaults()
    {
        Assert.Equal("İlkokul", SettlementCatalog.NormalizeSchoolType("  "));
        Assert.Equal("Lise", SettlementCatalog.NormalizeSchoolType("lise"));
        Assert.Equal("Park", SettlementCatalog.NormalizeAreaType(null));
        Assert.Equal("Meydan", SettlementCatalog.NormalizeAreaType("Meydan"));
        Assert.True(SettlementCatalog.IsSchoolType("Ortaokul"));
        Assert.False(SettlementCatalog.IsAreaType("Okul bahçesi"));
    }
}

public class SettlementPlaceMatchTests
{
    [Fact]
    public void Strong_match_equals_or_prefix()
    {
        Assert.True(SettlementPlaceMatch.IsStrong("Acaroba", "Acaroba"));
        Assert.True(SettlementPlaceMatch.IsStrong("acar", "Acaroba Mahallesi"));
        Assert.False(SettlementPlaceMatch.IsStrong("oba", "Acaroba"));
        Assert.False(SettlementPlaceMatch.IsStrong("a", "Acaroba"));
    }

    [Fact]
    public void Loose_match_contains()
    {
        Assert.True(SettlementPlaceMatch.IsLoose("oba", "Acaroba"));
        Assert.True(SettlementPlaceMatch.IsLoose("şehit", "Şehitkamil"));
        Assert.False(SettlementPlaceMatch.IsLoose("xyz", "Acaroba"));
    }
}

public class CoverageScaleTests
{
    [Fact]
    public void Matches_frontend_bands()
    {
        Assert.Equal("none", PersonelYonetim.Application.Features.Map.CoverageScale.Level(0, 80));
        Assert.Equal("low", PersonelYonetim.Application.Features.Map.CoverageScale.Level(1, 10));
        Assert.Equal("medium", PersonelYonetim.Application.Features.Map.CoverageScale.Level(1, 20));
        Assert.Equal("good", PersonelYonetim.Application.Features.Map.CoverageScale.Level(1, 50));
    }
}

public class RoleScopeMatrixTests
{
    [Fact]
    public void Administrative_officer_edits_own_staff_but_not_directorate_wide()
    {
        var codes = PersonelYonetim.Domain.Authorization.RolePermissionMatrix.GetMap()[
            PersonelYonetim.Domain.Authorization.RoleCodes.AdministrativeOfficer];

        Assert.Contains(PersonelYonetim.Domain.Authorization.PermissionCodes.EmployeesUpdate, codes);
        Assert.Contains(PersonelYonetim.Domain.Authorization.PermissionCodes.StockView, codes);
        Assert.Contains(PersonelYonetim.Domain.Authorization.PermissionCodes.EventsView, codes);
        Assert.DoesNotContain(PersonelYonetim.Domain.Authorization.PermissionCodes.EmployeesViewAllUnits, codes);
    }

    [Fact]
    public void Director_sees_all_units()
    {
        var codes = PersonelYonetim.Domain.Authorization.RolePermissionMatrix.GetMap()[
            PersonelYonetim.Domain.Authorization.RoleCodes.Director];

        Assert.Contains(PersonelYonetim.Domain.Authorization.PermissionCodes.EmployeesViewAllUnits, codes);
        Assert.Contains(PersonelYonetim.Domain.Authorization.PermissionCodes.EmployeesUpdate, codes);
    }
}
