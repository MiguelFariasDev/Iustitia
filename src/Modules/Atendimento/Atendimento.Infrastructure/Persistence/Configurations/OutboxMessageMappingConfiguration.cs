using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Atendimentos.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeia outbox_messages no AtendimentoDbContext e a EXCLUI das migrations deste módulo — a
/// tabela pertence ao Platform. Mesma razão e mesmo formato de
/// Legal.Infrastructure.Persistence.Configurations.OutboxMessageMappingConfiguration: as duas
/// precisam descrever a mesma tabela.
/// </summary>
public sealed class OutboxMessageMappingConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", table => table.ExcludeFromMigrations());

        builder.HasKey(outboxMessage => outboxMessage.Id);
        builder.Property(outboxMessage => outboxMessage.Id).ValueGeneratedNever();

        builder.Property(outboxMessage => outboxMessage.AggregateId).IsRequired();
        builder.Property(outboxMessage => outboxMessage.EventType).HasMaxLength(500).IsRequired();
        builder.Property(outboxMessage => outboxMessage.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(outboxMessage => outboxMessage.CreatedAt).IsRequired();

        builder.Property(outboxMessage => outboxMessage.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(outboxMessage => outboxMessage.RetryCount).IsRequired();
        builder.Property(outboxMessage => outboxMessage.LastError).HasMaxLength(2000);
        builder.Property(outboxMessage => outboxMessage.NextRetryAt);
    }
}
