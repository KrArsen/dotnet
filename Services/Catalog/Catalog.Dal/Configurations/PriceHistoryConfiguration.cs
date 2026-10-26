using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class PriceHistoryConfiguration : IEntityTypeConfiguration<PriceHistory>
{
    public void Configure(EntityTypeBuilder<PriceHistory> builder)
    {
        builder.ToTable("PriceHistory", "dbo", t =>
        {
            t.HasCheckConstraint("CK_PriceHistory_Price", "[Price] > 0");
            t.HasCheckConstraint("CK_PriceHistory_ValidTo", "[ValidTo] IS NULL OR [ValidTo] > [ValidFrom]");
        });

        builder.HasKey(ph => ph.Id);
        builder.Property(ph => ph.Id).ValueGeneratedOnAdd();

        builder.Property(ph => ph.Price)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(ph => ph.ValidFrom)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(ph => ph.ValidTo)
            .IsRequired(false);

        builder.Property(ph => ph.ChangedBy)
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue("system");

        builder.Property(ph => ph.Reason)
            .HasMaxLength(250)
            .IsRequired(false);

        builder.HasIndex(ph => new { ph.ProductId, ph.ValidFrom });
    }
}
