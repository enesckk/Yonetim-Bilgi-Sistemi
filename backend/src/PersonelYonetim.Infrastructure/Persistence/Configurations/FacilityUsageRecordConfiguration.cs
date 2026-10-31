using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonelYonetim.Domain.Entities;
namespace PersonelYonetim.Infrastructure.Persistence.Configurations;
public sealed class FacilityUsageRecordConfiguration : IEntityTypeConfiguration<FacilityUsageRecord>
{
    public void Configure(EntityTypeBuilder<FacilityUsageRecord> b)
    {
        b.ToTable("FacilityUsageRecords"); b.HasKey(x => x.Id);
        b.Property(x => x.SourceKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.SourceReference).HasMaxLength(300).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100); b.Property(x => x.UpdatedBy).HasMaxLength(100); b.Property(x => x.DeletedBy).HasMaxLength(100);
        b.HasIndex(x => x.SourceKey).IsUnique(); b.HasIndex(x => new { x.Year, x.Month }); b.HasIndex(x => x.FacilityId);
        b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
