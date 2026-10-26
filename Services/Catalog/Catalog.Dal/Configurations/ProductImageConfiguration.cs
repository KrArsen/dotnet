using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImage", "dbo");

        builder.HasKey(pi => pi.Id);
        builder.Property(pi => pi.Id).ValueGeneratedOnAdd();

        builder.Property(pi => pi.Url)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(pi => pi.AltText)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(pi => pi.IsMain)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(pi => pi.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.HasIndex(pi => pi.ProductId);
    }
}
