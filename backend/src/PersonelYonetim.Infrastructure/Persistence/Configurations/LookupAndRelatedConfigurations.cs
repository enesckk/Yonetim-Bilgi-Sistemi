using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence.Configurations;

public class EmploymentTypeConfiguration : IEntityTypeConfiguration<EmploymentType>
{
    public void Configure(EntityTypeBuilder<EmploymentType> builder)
    {
        builder.ToTable("EmploymentTypes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class FacilityCategoryConfiguration : IEntityTypeConfiguration<FacilityCategory>
{
    public void Configure(EntityTypeBuilder<FacilityCategory> builder)
    {
        builder.ToTable("FacilityCategories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.DeletedBy).HasMaxLength(100);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class JobTitleConfiguration : IEntityTypeConfiguration<JobTitle>
{
    public void Configure(EntityTypeBuilder<JobTitle> builder)
    {
        builder.ToTable("JobTitles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class JobDutyConfiguration : IEntityTypeConfiguration<JobDuty>
{
    public void Configure(EntityTypeBuilder<JobDuty> builder)
    {
        builder.ToTable("JobDuties");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Category);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class EmployeeAssignmentConfiguration : IEntityTypeConfiguration<EmployeeAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeAssignment> builder)
    {
        builder.ToTable("EmployeeAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.JobDuty)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.JobDutyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.EmployeeId, x.IsPrimary });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class EducationRecordConfiguration : IEntityTypeConfiguration<EducationRecord>
{
    public void Configure(EntityTypeBuilder<EducationRecord> builder)
    {
        builder.ToTable("EducationRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.University).HasMaxLength(200);
        builder.Property(x => x.Faculty).HasMaxLength(200);
        builder.Property(x => x.School).HasMaxLength(200);
        builder.Property(x => x.Department).HasMaxLength(200);
        builder.Property(x => x.Program).HasMaxLength(200);
        builder.Property(x => x.DiplomaNumber).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.DocumentPath).HasMaxLength(500);

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.EducationRecords)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
