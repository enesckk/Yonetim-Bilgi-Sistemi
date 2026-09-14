using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence;

public class AppDbContext : DbContext, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>();
    public DbSet<EmploymentType> EmploymentTypes => Set<EmploymentType>();
    public DbSet<FacilityCategory> FacilityCategories => Set<FacilityCategory>();
    public DbSet<JobTitle> JobTitles => Set<JobTitle>();
    public DbSet<JobDuty> JobDuties => Set<JobDuty>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeSensitiveData> EmployeeSensitiveData => Set<EmployeeSensitiveData>();
    public DbSet<EmployeeAssignment> EmployeeAssignments => Set<EmployeeAssignment>();
    public DbSet<EducationRecord> EducationRecords => Set<EducationRecord>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<EmployeeSkill> EmployeeSkills => Set<EmployeeSkill>();
    public DbSet<CertificateDefinition> CertificateDefinitions => Set<CertificateDefinition>();
    public DbSet<EmployeeCertificate> EmployeeCertificates => Set<EmployeeCertificate>();
    public DbSet<EmployeeNote> EmployeeNotes => Set<EmployeeNote>();
    public DbSet<EmployeeMovement> EmployeeMovements => Set<EmployeeMovement>();
    public DbSet<SpecialCondition> SpecialConditions => Set<SpecialCondition>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<SettlementPopulation> SettlementPopulations => Set<SettlementPopulation>();
    public DbSet<SettlementSchool> SettlementSchools => Set<SettlementSchool>();
    public DbSet<SettlementArea> SettlementAreas => Set<SettlementArea>();
    public DbSet<EventSettlement> EventSettlements => Set<EventSettlement>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();
    public DbSet<EventNote> EventNotes => Set<EventNote>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<WorkTaskAttachment> WorkTaskAttachments => Set<WorkTaskAttachment>();
    public DbSet<WorkTaskActivity> WorkTaskActivities => Set<WorkTaskActivity>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        if (Database.IsNpgsql())
        {
            // Supabase Data API exposes public by default; keep personnel tables outside it.
            modelBuilder.HasDefaultSchema("personel_app");
            foreach (var index in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetIndexes()))
            {
                var filter = index.GetFilter();
                if (filter is null)
                    continue;
                index.SetFilter(filter.Replace('[', '"').Replace(']', '"')
                    .Replace("\"IsDeleted\" = 0", "\"IsDeleted\" = FALSE", StringComparison.Ordinal));
            }
        }
        else
            modelBuilder.Ignore<DataProtectionKey>();
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ChangeTracker.DetectChanges();
        CorrectSpuriousModifiedStates();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        CorrectSpuriousModifiedStates();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// BaseEntity Id = Guid.NewGuid() ile oluşturulan kayıtlar navigation.Add ile eklenince
    /// DetectChanges onları Modified sanıyor → olmayan satıra UPDATE → concurrency hatası.
    /// Veritabanında yoksa Added'e çevir.
    /// </summary>
    private void CorrectSpuriousModifiedStates()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified).ToList())
        {
            // Tipik güncelleme az alan değiştirir; DetectChanges ile yanlış Modified'de neredeyse hepsi değişmiş görünür.
            var modifiedCount = entry.Properties.Count(p => p.IsModified && !p.Metadata.IsPrimaryKey());
            if (modifiedCount < 4)
                continue;

            var databaseValues = entry.GetDatabaseValues();
            if (databaseValues is null)
                entry.State = EntityState.Added;
        }
    }
}
