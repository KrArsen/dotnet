using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Product", "dbo", t =>
        {
            t.HasCheckConstraint("CK_Product_Price", "[Price] > 0");
            t.HasCheckConstraint("CK_Product_ItemType", "[ItemType] IN (N'Service', N'Product')");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedOnAdd();

        builder.Property(p => p.Sku)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(p => p.Sku)
            .IsUnique();

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(p => p.Name);

        builder.Property(p => p.ItemType)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("Service");

        builder.Property(p => p.Price)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.HasIndex(p => p.Price);

        builder.Property(p => p.StockQuantity)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(p => p.CreatedBy)
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue("system");

        builder.Property(p => p.UpdatedAt)
            .IsRequired(false);

        builder.Property(p => p.UpdatedBy)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        // 1:1 зв'язок з ProductDetail (ProductId = водночас PK і FK, ON DELETE CASCADE)
        builder.HasOne(p => p.Detail)
            .WithOne(d => d.Product)
            .HasForeignKey<ProductDetail>(d => d.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1:N зв'язок з ProductImage (ON DELETE CASCADE)
        builder.HasMany(p => p.Images)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1:N зв'язок з PriceHistory (ON DELETE CASCADE)
        builder.HasMany(p => p.PriceHistories)
            .WithOne(ph => ph.Product)
            .HasForeignKey(ph => ph.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // M:N зв'язок з Category через ProductCategory (ON DELETE CASCADE)
        builder.HasMany(p => p.ProductCategories)
            .WithOne(pc => pc.Product)
            .HasForeignKey(pc => pc.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
