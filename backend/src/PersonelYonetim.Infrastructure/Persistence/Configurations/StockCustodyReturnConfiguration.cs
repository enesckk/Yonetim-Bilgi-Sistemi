using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonelYonetim.Domain.Entities;
namespace PersonelYonetim.Infrastructure.Persistence.Configurations;
public sealed class StockCustodyReturnConfiguration : IEntityTypeConfiguration<StockCustodyReturn>
{
    public void Configure(EntityTypeBuilder<StockCustodyReturn> b)
    {
        b.ToTable("StockCustodyReturns");
        b.HasKey(x => x.Id);
        b.Property(x => x.Quantity).HasPrecision(18, 2);
        b.Property(x => x.Notes).HasMaxLength(400);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.DeletedBy).HasMaxLength(100);
        b.HasOne(x => x.StockCustody).WithMany(x => x.Returns).HasForeignKey(x => x.StockCustodyId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
