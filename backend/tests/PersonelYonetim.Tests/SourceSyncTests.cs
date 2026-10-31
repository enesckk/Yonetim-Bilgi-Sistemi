using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.SourceSync;
namespace PersonelYonetim.Tests;
public class SourceSyncTests
{
    private sealed class User(params string[] permissions) : ICurrentUserService
    {
        public Guid? UserId => null; public string UserName => "sync-test"; public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Permissions => permissions; public bool HasPermission(string code) => permissions.Contains(code);
    }
    private static readonly User Admin = new(PermissionCodes.EventsManage, PermissionCodes.OrganizationManage, PermissionCodes.EmployeesViewAllUnits);
    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection = null!; public AppDbContext Db = null!; public MemoryCache Cache = new(new MemoryCacheOptions());
        public SourceSyncService Service => new(Db, Admin, Cache);
        public SourceSyncInput Input = null!;
        public Guid ProtectedId;
        public static async Task<Fixture> New()
        {
            var f = new Fixture { Connection = new("Data Source=:memory:") }; await f.Connection.OpenAsync();
            f.Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(f.Connection).Options); await f.Db.Database.EnsureCreatedAsync();
            var directorate = new OrganizationUnit { Code="KSSIM", Name="Kültür, Sanat ve Sosyal İşler Müdürlüğü", Type=OrganizationUnitType.Directorate };
            f.Db.OrganizationUnits.Add(directorate);
            f.Db.OrganizationUnits.Add(new OrganizationUnit { Code="DTSS", Name="Devlet Tiyatroları Şehitkamil Sahnesi", Type=OrganizationUnitType.Facility, Parent=directorate });
            f.Db.Settlements.Add(new Settlement { OfficialCode="TEST", Name="Test Mahallesi", HeadmanName="Önceki", HeadmanPhone="Eski" });
            var protectedEvent = new Event { Title="BilimFest 2026", Status=EventStatus.Completed, Category=EventCategories.Project, Description="Kullanıcının özgün açıklaması", StartAtUtc=new(2026,10,15), ActualAttendees=57678 };
            f.Db.Events.Add(protectedEvent); f.ProtectedId=protectedEvent.Id;
            await f.Db.SaveChangesAsync();
            f.Input = new([new("TEST","Test Mahallesi","Yeni Muhtar","05000000001",100,49,51,2025)],
                [new(Guid.NewGuid(),"Devlet Tiyatrosu Şehitkamil Sahnesi","TEST","bakimda","Kaynak Yetkili","05000000002"),new(Guid.NewGuid(),"Yeni Tesis","TEST","aktif","İkinci Yetkili","05000000003")],
                [new(Guid.NewGuid(),"Erzin Kampı","TEST","2024-07-03","Kaynakta verilen açıklama",10)]);
            return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); Cache.Dispose(); }
    }
    [Fact]
    public async Task Preview_is_read_only_apply_is_atomic_idempotent_and_preserves_Bilimfest_and_existing_facility_identity()
    {
        await using var f=await Fixture.New();
        var originalFacility=(await f.Db.OrganizationUnits.SingleAsync(u=>u.Code=="DTSS")).Id;
        var preview=await f.Service.PreviewAsync(f.Input);
        Assert.False(preview.AlreadyApplied); Assert.NotEmpty(preview.Changes);
        Assert.Equal("Önceki",(await f.Db.Settlements.SingleAsync()).HeadmanName); Assert.Equal(2,await f.Db.OrganizationUnits.CountAsync()); Assert.Equal(1,await f.Db.Events.CountAsync());
        await f.Service.ApplyAsync(new(f.Input,preview.ReviewToken)); f.Db.ChangeTracker.Clear();
        Assert.Equal("Yeni Muhtar",(await f.Db.Settlements.SingleAsync()).HeadmanName); Assert.Equal(100,(await f.Db.SettlementPopulations.SingleAsync()).Population);
        var unit=await f.Db.OrganizationUnits.SingleAsync(u=>u.Code=="DTSS"); Assert.Equal(originalFacility,unit.Id); Assert.Equal(OrganizationUnitStatus.UnderRenovation,unit.Status); Assert.Equal("Kaynak Yetkili",unit.ContactName);
        Assert.Equal(3,await f.Db.OrganizationUnits.CountAsync()); Assert.Equal(2,await f.Db.Events.CountAsync());
        var original=await f.Db.Events.SingleAsync(e=>e.Id==f.ProtectedId); Assert.Equal("Kullanıcının özgün açıklaması",original.Description); Assert.Equal(57678,original.ActualAttendees);
        Assert.Single(await f.Db.EventSettlements.ToListAsync()); Assert.NotEmpty(await f.Db.AuditLogs.Where(a=>a.Action=="SourceSync").ToListAsync());
        unit.ContactName="Sonraki manuel düzenleme"; await f.Db.SaveChangesAsync();
        Assert.True((await f.Service.ApplyAsync(new(f.Input,preview.ReviewToken))).AlreadyApplied);
        Assert.Equal("Sonraki manuel düzenleme",(await f.Db.OrganizationUnits.SingleAsync(u=>u.Code=="DTSS")).ContactName);
        Assert.Equal(2,await f.Db.Events.CountAsync());
    }
    [Fact]
    public async Task Stale_preview_or_ambiguous_facility_never_partially_updates_data()
    {
        await using var f=await Fixture.New(); var p=await f.Service.PreviewAsync(f.Input);
        var settlement=await f.Db.Settlements.SingleAsync(); settlement.HeadmanName="Başka kullanıcının değişikliği";await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationException>(()=>f.Service.ApplyAsync(new(f.Input,p.ReviewToken)));
        f.Db.ChangeTracker.Clear(); Assert.Equal("Başka kullanıcının değişikliği",(await f.Db.Settlements.SingleAsync()).HeadmanName);Assert.Equal(1,await f.Db.Events.CountAsync());Assert.Empty(await f.Db.SettlementPopulations.ToListAsync());
        f.Db.OrganizationUnits.Add(new OrganizationUnit { Name="Devlet Tiyatrosu Şehitkamil Sahnesi",Type=OrganizationUnitType.Facility });await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationException>(()=>f.Service.PreviewAsync(f.Input));
        f.Db.ChangeTracker.Clear();Assert.Equal("Başka kullanıcının değişikliği",(await f.Db.Settlements.SingleAsync()).HeadmanName);
    }
    [Fact]
    public async Task Missing_permissions_invalid_population_and_non_Erzin_events_are_rejected()
    {
        await using var f=await Fixture.New();
        var denied=new SourceSyncService(f.Db,new User(PermissionCodes.EventsManage),f.Cache);
        await Assert.ThrowsAsync<ForbiddenException>(()=>denied.PreviewAsync(f.Input));
        await Assert.ThrowsAsync<ValidationException>(()=>f.Service.PreviewAsync(f.Input with { Neighborhoods=[f.Input.Neighborhoods[0] with { Population=101 }] }));
        await Assert.ThrowsAsync<ValidationException>(()=>f.Service.PreviewAsync(f.Input with { Events=[f.Input.Events[0] with { Title="BilimFest 2026" }] }));
    }
}
