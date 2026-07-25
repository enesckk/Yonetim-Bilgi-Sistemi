using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence.Configurations;

public class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => new { x.Category, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class EmployeeSkillConfiguration : IEntityTypeConfiguration<EmployeeSkill>
{
    public void Configure(EntityTypeBuilder<EmployeeSkill> builder)
    {
        builder.ToTable("EmployeeSkills");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExperienceDuration).HasMaxLength(100);
        builder.Property(x => x.CertificateIssuer).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.Skills)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Skill)
            .WithMany(x => x.EmployeeSkills)
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.EmployeeId, x.SkillId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class CertificateDefinitionConfiguration : IEntityTypeConfiguration<CertificateDefinition>
{
    public void Configure(EntityTypeBuilder<CertificateDefinition> builder)
    {
        builder.ToTable("CertificateDefinitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class EmployeeCertificateConfiguration : IEntityTypeConfiguration<EmployeeCertificate>
{
    public void Configure(EntityTypeBuilder<EmployeeCertificate> builder)
    {
        builder.ToTable("EmployeeCertificates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Issuer).HasMaxLength(200);
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.DocumentNumber).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.DocumentPath).HasMaxLength(500);

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.Certificates)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CertificateDefinition)
            .WithMany(x => x.EmployeeCertificates)
            .HasForeignKey(x => x.CertificateDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.RelatedSkill)
            .WithMany()
            .HasForeignKey(x => x.RelatedSkillId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ExpiresOn);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class EmployeeNoteConfiguration : IEntityTypeConfiguration<EmployeeNote>
{
    public void Configure(EntityTypeBuilder<EmployeeNote> builder)
    {
        builder.ToTable("EmployeeNotes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Content).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.AttachmentPath).HasMaxLength(500);

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.Notes)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.EmployeeId, x.Visibility });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class EmployeeMovementConfiguration : IEntityTypeConfiguration<EmployeeMovement>
{
    public void Configure(EntityTypeBuilder<EmployeeMovement> builder)
    {
        builder.ToTable("EmployeeMovements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.ApprovedBy).HasMaxLength(150);

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.Movements)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.EmployeeId, x.StartDate });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SpecialConditionConfiguration : IEntityTypeConfiguration<SpecialCondition>
{
    public void Configure(EntityTypeBuilder<SpecialCondition> builder)
    {
        builder.ToTable("SpecialConditions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ConditionType).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.DocumentPath).HasMaxLength(500);

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.SpecialConditions)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
