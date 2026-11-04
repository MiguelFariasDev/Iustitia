using Advocacia.Modules.Legal.Domain.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Configurations;

/// <summary>
/// Tabela <c>ai_usage_logs</c>: auditoria de TODA chamada ao provedor de IA — tokens, custo,
/// latência e desfecho, sem conteúdo (ver ADR-067). Append-only. Filtro global de tenant via
/// IHasTenant; RLS em 014_publication_suggestions.sql.
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

        builder.Property(log => log.Purpose).HasMaxLength(30).IsRequired();
        builder.Property(log => log.Provider).HasMaxLength(30).IsRequired();
        builder.Property(log => log.Model).HasMaxLength(100).IsRequired();
        builder.Property(log => log.PromptVersion).HasMaxLength(20).IsRequired();

        builder.Property(log => log.ReferenceId);

        builder.Property(log => log.InputTokens).IsRequired();
        builder.Property(log => log.OutputTokens).IsRequired();
        builder.Property(log => log.CacheCreationInputTokens).IsRequired();
        builder.Property(log => log.CacheReadInputTokens).IsRequired();

        // 6 casas: uma chamada de classificação custa frações de centavo de dólar.
        builder.Property(log => log.CostUsd).HasColumnType("numeric(12,6)").IsRequired();

        builder.Property(log => log.LatencyMs).IsRequired();

        builder.Property(log => log.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

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
