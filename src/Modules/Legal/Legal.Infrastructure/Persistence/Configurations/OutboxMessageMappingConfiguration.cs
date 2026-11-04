using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeia outbox_messages no LegalDbContext e a EXCLUI das migrations deste módulo.
///
/// O mapeamento é necessário porque o OutboxInterceptor grava os eventos de domínio de
/// Legal via <c>context.Set&lt;OutboxMessage&gt;()</c>, na mesma transação do SaveChanges —
/// sem isso, o interceptor lançaria por tipo não mapeado e nenhum evento de publicação
/// chegaria à outbox (ver ADR-008).
///
/// A exclusão das migrations é obrigatória porque a tabela pertence ao Platform, que já a
/// cria em sua própria migration; sem ExcludeFromMigrations a migration de Legal tentaria
/// criar a mesma tabela e falharia na aplicação. A forma das colunas é ditada por
/// Platform.Infrastructure.Persistence.Configurations.OutboxMessageConfiguration — as duas
/// precisam descrever a mesma tabela, então qualquer mudança lá deve ser espelhada aqui.
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
