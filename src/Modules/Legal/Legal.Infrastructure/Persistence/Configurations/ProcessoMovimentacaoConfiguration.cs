using Advocacia.Modules.Legal.Domain.Movimentacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Configurations;

/// <summary>
/// Andamentos capturados (ADR-075). Sem FK para processos, pelo mesmo motivo de
/// <see cref="ProcessoConfiguration"/>; a RLS por tenant vem do 021_movimentacoes_notificacoes.sql.
/// </summary>
public sealed class ProcessoMovimentacaoConfiguration : IEntityTypeConfiguration<ProcessoMovimentacao>
{
    public void Configure(EntityTypeBuilder<ProcessoMovimentacao> builder)
    {
        builder.ToTable("processo_movimentacoes");

        builder.HasKey(movimentacao => movimentacao.Id);
        builder.Property(movimentacao => movimentacao.Id)
            .HasConversion(id => id.Value, value => ProcessoMovimentacaoId.From(value))
            .ValueGeneratedNever();

        builder.Property(movimentacao => movimentacao.TenantId).IsRequired();
        builder.Property(movimentacao => movimentacao.ProcessoId).IsRequired();
        builder.Property(movimentacao => movimentacao.ExternalId).HasMaxLength(ProcessoMovimentacao.ExternalIdMaxLength).IsRequired();
        builder.Property(movimentacao => movimentacao.Fonte).HasMaxLength(ProcessoMovimentacao.FonteMaxLength).IsRequired();
        builder.Property(movimentacao => movimentacao.Nome).HasMaxLength(ProcessoMovimentacao.NomeMaxLength).IsRequired();
        builder.Property(movimentacao => movimentacao.Complemento).HasMaxLength(ProcessoMovimentacao.ComplementoMaxLength);
        builder.Property(movimentacao => movimentacao.Instancia).HasMaxLength(ProcessoMovimentacao.InstanciaMaxLength);
        builder.Property(movimentacao => movimentacao.CapturadaEm).IsRequired();

        builder.Property(movimentacao => movimentacao.CreatedBy).HasMaxLength(100);
        builder.Property(movimentacao => movimentacao.UpdatedBy).HasMaxLength(100);

        // A deduplicação: reconsultar a mesma janela nunca grava o mesmo andamento duas vezes.
        builder.HasIndex(movimentacao => new { movimentacao.TenantId, movimentacao.ProcessoId, movimentacao.ExternalId })
            .IsUnique()
            .HasDatabaseName("ux_processo_movimentacoes_tenant_processo_external");

        // Timeline do processo e "último andamento" da janela do job.
        builder.HasIndex(movimentacao => new { movimentacao.TenantId, movimentacao.ProcessoId, movimentacao.DataHora })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_processo_movimentacoes_tenant_processo_data");

        builder.HasIndex(movimentacao => new { movimentacao.TenantId, movimentacao.ProcessoId, movimentacao.CapturadaEm })
            .HasDatabaseName("ix_processo_movimentacoes_tenant_processo_capturada");
    }
}
