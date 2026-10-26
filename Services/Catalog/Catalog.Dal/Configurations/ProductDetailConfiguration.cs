using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class ProductDetailConfiguration : IEntityTypeConfiguration<ProductDetail>
{
    public void Configure(EntityTypeBuilder<ProductDetail> builder)
    {
        builder.ToTable("ProductDetail", "dbo");

        builder.HasKey(pd => pd.ProductId);
        builder.Property(pd => pd.ProductId).ValueGeneratedNever();

        builder.Property(pd => pd.Description)
            .IsRequired();

        builder.Property(pd => pd.Unit)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("послуга");

        builder.Property(pd => pd.DurationMinutes)
            .IsRequired(false);

        builder.Property(pd => pd.TaxRate)
            .HasColumnType("decimal(5,2)")
            .IsRequired()
            .HasDefaultValue(20.00m);

        builder.Property(pd => pd.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(pd => pd.CreatedBy)
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue("system");

        builder.Property(pd => pd.UpdatedAt)
            .IsRequired(false);

        builder.Property(pd => pd.UpdatedBy)
            .HasMaxLength(100)
            .IsRequired(false);
    }
}
