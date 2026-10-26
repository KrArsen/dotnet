using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("ProductCategory", "dbo");

        // Складений первинний ключ
        builder.HasKey(pc => new { pc.ProductId, pc.CategoryId });

        // Власне поле проміжної сутності M:N з дефолтним значенням SYSUTCDATETIME()
        builder.Property(pc => pc.AddedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(pc => pc.CategoryId);
    }
}
