using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence.Configurations;

public class StockCustodyConfiguration : IEntityTypeConfiguration<StockCustody>
{
    public void Configure(EntityTypeBuilder<StockCustody> b)
    {
        b.ToTable("StockCustodies");
        b.HasKey(x => x.Id);
        b.Property(x => x.Quantity).HasPrecision(18, 2);
        b.Property(x => x.SerialNumber).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(400);
        b.Property(x => x.ReturnNotes).HasMaxLength(400);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.DeletedBy).HasMaxLength(100);
        b.HasOne(x => x.StockItem).WithMany().HasForeignKey(x => x.StockItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Replaces).WithMany().HasForeignKey(x => x.ReplacesId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.StockItemId, x.LocationId, x.ReturnedOn });
        b.HasIndex(x => new { x.EmployeeId, x.ReturnedOn });
        b.HasIndex(x => new { x.StockItemId, x.SerialNumber }).IsUnique()
            .HasFilter("[SerialNumber] IS NOT NULL AND [ReturnedOn] IS NULL AND [IsDeleted] = 0");
        b.HasIndex(x => x.ReplacesId).IsUnique().HasFilter("[ReplacesId] IS NOT NULL");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
