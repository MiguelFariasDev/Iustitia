using Advocacia.Modules.Legal.Domain.Movimentacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Configurations;

/// <summary>
/// Avisos ao cliente (ADR-075). O CHECK "Enviada exige revisor e data de envio" e a RLS ficam no
/// 021_movimentacoes_notificacoes.sql — o EF não modela CHECK com regra entre colunas.
/// </summary>
public sealed class NotificacaoClienteConfiguration : IEntityTypeConfiguration<NotificacaoCliente>
{
    public void Configure(EntityTypeBuilder<NotificacaoCliente> builder)
    {
        builder.ToTable("notificacoes_cliente");

        builder.HasKey(notificacao => notificacao.Id);
        builder.Property(notificacao => notificacao.Id)
            .HasConversion(id => id.Value, value => NotificacaoClienteId.From(value))
            .ValueGeneratedNever();

        builder.Property(notificacao => notificacao.TenantId).IsRequired();
        builder.Property(notificacao => notificacao.ProcessoId).IsRequired();
        builder.Property(notificacao => notificacao.ClienteId).IsRequired();
        builder.Property(notificacao => notificacao.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(notificacao => notificacao.Mensagem).HasMaxLength(NotificacaoCliente.MensagemMaxLength).IsRequired();
        builder.Property(notificacao => notificacao.AndamentosDesde).IsRequired();

        builder.Property(notificacao => notificacao.CreatedBy).HasMaxLength(100);
        builder.Property(notificacao => notificacao.UpdatedBy).HasMaxLength(100);

        // No máximo UM rascunho pendente por processo: as noites seguintes somam a ele.
        builder.HasIndex(notificacao => new { notificacao.TenantId, notificacao.ProcessoId })
            .IsUnique()
            .HasFilter("status = 'PendenteRevisao'")
            .HasDatabaseName("ux_notificacoes_cliente_pendente_por_processo");

        builder.HasIndex(notificacao => new { notificacao.TenantId, notificacao.Status, notificacao.CreatedAt })
            .HasDatabaseName("ix_notificacoes_cliente_tenant_status_criacao");
    }
}
