using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Platform.Infrastructure.Persistence.Configurations;

/// <summary>OutboxMessage não implementa IHasTenant nem ISoftDeletable: a outbox é global.</summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.AggregateId).IsRequired();

        builder.Property(o => o.EventType).HasMaxLength(500).IsRequired();

        builder.Property(o => o.Payload).HasColumnType("jsonb").IsRequired();

        builder.Property(o => o.CreatedAt).IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.RetryCount).IsRequired();

        builder.Property(o => o.LastError).HasMaxLength(2000);

        builder.Property(o => o.NextRetryAt);

        builder.HasIndex(o => o.CreatedAt)
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("ix_outbox_pending");

        builder.HasIndex(o => new { o.Status, o.CreatedAt })
            .HasDatabaseName("ix_outbox_status_created");
    }
}
