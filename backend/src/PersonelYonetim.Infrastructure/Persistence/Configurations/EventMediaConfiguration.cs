using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonelYonetim.Domain.Entities;
namespace PersonelYonetim.Infrastructure.Persistence.Configurations;
public sealed class EventMediaConfiguration : IEntityTypeConfiguration<EventMedia>
{
    public void Configure(EntityTypeBuilder<EventMedia> b)
    {
        b.ToTable("EventMedia"); b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(10).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(250).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.FileData).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.DeletedBy).HasMaxLength(100);
        b.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.EventId);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
