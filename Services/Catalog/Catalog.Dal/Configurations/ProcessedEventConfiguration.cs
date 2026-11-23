using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("ProcessedEvents", "dbo");
        builder.HasKey(e => e.EventId);

        builder.Property(e => e.EventId)
            .ValueGeneratedNever();

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(e => e.ProcessedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", "dbo");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(e => e.Exchange)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.RoutingKey)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(e => e.Payload)
            .IsRequired();

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(128);

        builder.Property(e => e.OccurredAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(e => e.ProcessedAt)
            .HasDatabaseName("IX_OutboxMessages_ProcessedAt");
    }
}
