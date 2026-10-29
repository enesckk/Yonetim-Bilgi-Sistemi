using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence.Configurations;

public class InformationNoteConfiguration : IEntityTypeConfiguration<InformationNote>
{
    public void Configure(EntityTypeBuilder<InformationNote> b)
    {
        b.ToTable("InformationNotes"); b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.DateText).HasMaxLength(100).IsRequired();
        b.Property(x => x.Organizer).HasMaxLength(300).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        b.Property(x => x.PreparedBy).HasMaxLength(300).IsRequired();
        b.Property(x => x.Body).HasMaxLength(30000).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(250);
        b.Property(x => x.ContentType).HasMaxLength(100);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.DeletedBy).HasMaxLength(100);
        b.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.EventId); b.HasIndex(x => x.CreatedAtUtc);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
