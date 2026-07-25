using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence;

public class AppDbContext : DbContext
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
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
