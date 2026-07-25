using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EmployeeNumber).HasMaxLength(50);
        builder.Property(x => x.PhotoPath).HasMaxLength(500);
        builder.Property(x => x.PersonalPhone).HasMaxLength(30);
        builder.Property(x => x.CorporatePhone).HasMaxLength(30);
        builder.Property(x => x.PersonalEmail).HasMaxLength(200);
        builder.Property(x => x.CorporateEmail).HasMaxLength(200);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.EmergencyContactName).HasMaxLength(150);
        builder.Property(x => x.EmergencyContactPhone).HasMaxLength(30);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.DeletedBy).HasMaxLength(100);

        builder.Ignore(x => x.FullName);

        builder.HasIndex(x => x.EmployeeNumber).IsUnique().HasFilter("[EmployeeNumber] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => new { x.LastName, x.FirstName });
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.UnitId);
        builder.HasIndex(x => x.FacilityId);

        // Birim ile tesis aynı OrganizationUnits tablosuna bağlanır; cascade yok.
        builder.HasOne(x => x.Unit)
            .WithMany(x => x.EmployeesInUnit)
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Facility)
            .WithMany(x => x.EmployeesAtFacility)
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EmploymentType)
            .WithMany(x => x.Employees)
            .HasForeignKey(x => x.EmploymentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.JobTitle)
            .WithMany(x => x.Employees)
            .HasForeignKey(x => x.JobTitleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ManagerEmployee)
            .WithMany()
            .HasForeignKey(x => x.ManagerEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SensitiveData)
            .WithOne(x => x.Employee)
            .HasForeignKey<EmployeeSensitiveData>(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
