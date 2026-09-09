using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence.Configurations;

public class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.ToTable("WorkTasks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.ReviewNote).HasMaxLength(2000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.DeletedBy).HasMaxLength(100);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssigneeUser)
            .WithMany()
            .HasForeignKey(x => x.AssigneeUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReviewerUser)
            .WithMany()
            .HasForeignKey(x => x.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Unit)
            .WithMany()
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.AssigneeUserId);
        builder.HasIndex(x => x.CreatedByUserId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class WorkTaskAttachmentConfiguration : IEntityTypeConfiguration<WorkTaskAttachment>
{
    public void Configure(EntityTypeBuilder<WorkTaskAttachment> builder)
    {
        builder.ToTable("WorkTaskAttachments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RelativePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.DeletedBy).HasMaxLength(100);

        builder.HasOne(x => x.WorkTask)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.WorkTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.UploadedByUser)
            .WithMany()
            .HasForeignKey(x => x.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class WorkTaskActivityConfiguration : IEntityTypeConfiguration<WorkTaskActivity>
{
    public void Configure(EntityTypeBuilder<WorkTaskActivity> builder)
    {
        builder.ToTable("WorkTaskActivities");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(2000);

        builder.HasOne(x => x.WorkTask)
            .WithMany(x => x.Activities)
            .HasForeignKey(x => x.WorkTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.WorkTaskId, x.AtUtc });
    }
}
