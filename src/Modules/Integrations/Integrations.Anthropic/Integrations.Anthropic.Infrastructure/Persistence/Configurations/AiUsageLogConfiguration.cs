using Advocacia.Modules.Integrations.Anthropic.Domain.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure.Persistence.Configurations;

/// <summary>
/// Tabela <c>ai_usage_logs</c>: auditoria de TODA chamada à IA — tokens, custo, latência e
/// desfecho, sem conteúdo (ADR-069). Append-only. Filtro global de tenant via IHasTenant; RLS,
/// CHECKs e grants em 015_ai_usage_logs.sql.
/// </summary>
public sealed class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.ToTable("ai_usage_logs");

        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id)
            .HasConversion(id => id.Value, value => AiUsageLogId.From(value))
            .ValueGeneratedNever();

        builder.Property(log => log.TenantId).IsRequired();
        builder.Property(log => log.UserId);

        builder.Property(log => log.Model).HasMaxLength(100).IsRequired();
        builder.Property(log => log.Purpose).HasMaxLength(30);
        builder.Property(log => log.PromptVersion).HasMaxLength(20);
        builder.Property(log => log.ReferenceId);

        builder.Property(log => log.InputTokens).IsRequired();
        builder.Property(log => log.OutputTokens).IsRequired();
        builder.Property(log => log.CacheCreationInputTokens).IsRequired();
        builder.Property(log => log.CacheReadInputTokens).IsRequired();

        // 6 casas: uma chamada de classificação custa frações de centavo de dólar.
        builder.Property(log => log.CostUsd).HasColumnType("numeric(12,6)").IsRequired();

        builder.Property(log => log.LatencyMs).IsRequired();
        builder.Property(log => log.Success).IsRequired();
        builder.Property(log => log.ErrorCode).HasMaxLength(60);
        builder.Property(log => log.RequestId).HasMaxLength(100);
        builder.Property(log => log.CreatedAt).IsRequired();

        builder.HasIndex(log => new { log.TenantId, log.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_ai_usage_logs_tenant_created");

        builder.HasIndex(log => log.ReferenceId)
            .HasFilter("reference_id IS NOT NULL")
            .HasDatabaseName("ix_ai_usage_logs_reference");
    }
}
