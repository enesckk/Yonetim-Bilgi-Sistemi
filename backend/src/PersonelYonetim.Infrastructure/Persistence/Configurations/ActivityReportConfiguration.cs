using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonelYonetim.Domain.Entities;
namespace PersonelYonetim.Infrastructure.Persistence.Configurations;
public sealed class ActivityReportConfiguration : IEntityTypeConfiguration<ActivityReport>
{
    public void Configure(EntityTypeBuilder<ActivityReport> b)
    {
        b.ToTable("ActivityReports"); b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.DateBasis).HasMaxLength(10).IsRequired();
        b.Property(x => x.Kind).HasMaxLength(10).IsRequired();
        b.Property(x => x.BodyJson).IsRequired();
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.CreatedBy).HasMaxLength(100); b.Property(x => x.UpdatedBy).HasMaxLength(100); b.Property(x => x.DeletedBy).HasMaxLength(100);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
public sealed class ActivityReportAssetConfiguration : IEntityTypeConfiguration<ActivityReportAsset>
{
    public void Configure(EntityTypeBuilder<ActivityReportAsset> b)
    {
        b.ToTable("ActivityReportAssets"); b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(250).IsRequired(); b.Property(x => x.ContentType).HasMaxLength(100).IsRequired(); b.Property(x => x.FileData).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100); b.Property(x => x.UpdatedBy).HasMaxLength(100); b.Property(x => x.DeletedBy).HasMaxLength(100);
        b.HasOne(x => x.Report).WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => x.ReportId);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
