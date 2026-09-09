using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence.Configurations;

public class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.ToTable("Settlements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OfficialCode).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.SettlementType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.HeadmanName).HasMaxLength(120);
        builder.Property(x => x.HeadmanPhone).HasMaxLength(30);
        builder.Property(x => x.GeometryJson);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.DeletedBy).HasMaxLength(100);

        builder.HasIndex(x => x.OfficialCode).IsUnique();
        builder.HasIndex(x => x.Name);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SettlementSchoolConfiguration : IEntityTypeConfiguration<SettlementSchool>
{
    public void Configure(EntityTypeBuilder<SettlementSchool> builder)
    {
        builder.ToTable("SettlementSchools");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SchoolType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PrincipalName).HasMaxLength(120);
        builder.Property(x => x.PrincipalPhone).HasMaxLength(30);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.DeletedBy).HasMaxLength(100);

        builder.HasOne(x => x.Settlement)
            .WithMany(x => x.Schools)
            .HasForeignKey(x => x.SettlementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SettlementId, x.Name }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SettlementAreaConfiguration : IEntityTypeConfiguration<SettlementArea>
{
    public void Configure(EntityTypeBuilder<SettlementArea> builder)
    {
        builder.ToTable("SettlementAreas");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AreaType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(300);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.DeletedBy).HasMaxLength(100);

        builder.HasOne(x => x.Settlement)
            .WithMany(x => x.Areas)
            .HasForeignKey(x => x.SettlementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SettlementId, x.Name }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SettlementPopulationConfiguration : IEntityTypeConfiguration<SettlementPopulation>
{
    public void Configure(EntityTypeBuilder<SettlementPopulation> builder)
    {
        builder.ToTable("SettlementPopulations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Source).HasMaxLength(80).IsRequired();
        builder.Property(x => x.SourceReference).HasMaxLength(300);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.DeletedBy).HasMaxLength(100);

        builder.HasOne(x => x.Settlement)
            .WithMany(x => x.Populations)
            .HasForeignKey(x => x.SettlementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SettlementId, x.Year }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class EventSettlementConfiguration : IEntityTypeConfiguration<EventSettlement>
{
    public void Configure(EntityTypeBuilder<EventSettlement> builder)
    {
        builder.ToTable("EventSettlements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.DeletedBy).HasMaxLength(100);

        builder.HasOne(x => x.Event)
            .WithMany(x => x.Settlements)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Settlement)
            .WithMany(x => x.EventLinks)
            .HasForeignKey(x => x.SettlementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.EventId, x.SettlementId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class DirectMessageConfiguration : IEntityTypeConfiguration<DirectMessage>
{
    public void Configure(EntityTypeBuilder<DirectMessage> builder)
    {
        builder.ToTable("DirectMessages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.AttachmentPath).HasMaxLength(500);
        builder.Property(x => x.AttachmentFileName).HasMaxLength(255);
        builder.Property(x => x.AttachmentContentType).HasMaxLength(120);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.DeletedBy).HasMaxLength(100);

        builder.HasOne(x => x.Sender)
            .WithMany()
            .HasForeignKey(x => x.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Recipient)
            .WithMany()
            .HasForeignKey(x => x.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RelatedEvent)
            .WithMany()
            .HasForeignKey(x => x.RelatedEventId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.SenderUserId, x.RecipientUserId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.RecipientUserId, x.ReadAtUtc });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class EventNoteConfiguration : IEntityTypeConfiguration<EventNote>
{
    public void Configure(EntityTypeBuilder<EventNote> builder)
    {
        builder.ToTable("EventNotes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.DeletedBy).HasMaxLength(100);

        builder.HasOne(x => x.Event)
            .WithMany(x => x.Notes)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Author)
            .WithMany()
            .HasForeignKey(x => x.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.EventId, x.CreatedAtUtc });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
