using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Platform.Infrastructure.Persistence.Configurations;

/// <summary>ProcessedEvent não implementa IHasTenant nem ISoftDeletable: idempotência é global por consumer.</summary>
public sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("processed_events");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.EventId).IsRequired();
        builder.Property(p => p.ConsumerName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.ProcessedAt).IsRequired();

        builder.HasIndex(p => new { p.EventId, p.ConsumerName })
            .IsUnique()
            .HasDatabaseName("ix_processed_events_event_consumer");

        builder.HasIndex(p => p.ProcessedAt)
            .HasDatabaseName("ix_processed_events_processed_at");
    }
}
