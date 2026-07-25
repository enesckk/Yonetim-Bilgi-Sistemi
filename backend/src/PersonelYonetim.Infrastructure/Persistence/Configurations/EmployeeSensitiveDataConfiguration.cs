using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonelYonetim.Domain.Entities;

namespace PersonelYonetim.Infrastructure.Persistence.Configurations;

public class EmployeeSensitiveDataConfiguration : IEntityTypeConfiguration<EmployeeSensitiveData>
{
    public void Configure(EntityTypeBuilder<EmployeeSensitiveData> builder)
    {
        builder.ToTable("EmployeeSensitiveData");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.EmployeeId).IsUnique();
        builder.Property(x => x.NationalIdEncrypted).HasMaxLength(500);
        builder.Property(x => x.NationalIdHash).HasMaxLength(64);
        builder.HasIndex(x => x.NationalIdHash);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.DeletedBy).HasMaxLength(100);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
