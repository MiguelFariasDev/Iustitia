using Advocacia.Modules.Financeiro.Domain.Lancamentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Financeiro.Infrastructure.Persistence.Configurations;

public sealed class LancamentoConfiguration : IEntityTypeConfiguration<Lancamento>
{
    public void Configure(EntityTypeBuilder<Lancamento> builder)
    {
        builder.ToTable("lancamentos");

        builder.HasKey(lancamento => lancamento.Id);
        builder.Property(lancamento => lancamento.Id)
            .HasConversion(id => id.Value, value => LancamentoId.From(value))
            .ValueGeneratedNever();

        builder.Property(lancamento => lancamento.TenantId).IsRequired();

        builder.Property(lancamento => lancamento.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(lancamento => lancamento.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(lancamento => lancamento.Descricao).HasMaxLength(Lancamento.DescricaoMaxLength).IsRequired();
        builder.Property(lancamento => lancamento.Pessoa).HasMaxLength(Lancamento.PessoaMaxLength);
        builder.Property(lancamento => lancamento.Processo).HasMaxLength(Lancamento.ProcessoMaxLength);

        // numeric(18,2): dinheiro nunca é double — perderia centavos nos somatórios.
        builder.Property(lancamento => lancamento.Valor).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(lancamento => lancamento.Vencimento).HasColumnType("date").IsRequired();
        builder.Property(lancamento => lancamento.PagoEm);

        builder.Property(lancamento => lancamento.CreatedAt).IsRequired();
        builder.Property(lancamento => lancamento.CreatedBy).HasMaxLength(100);
        builder.Property(lancamento => lancamento.UpdatedBy).HasMaxLength(100);
        builder.Property(lancamento => lancamento.DeletedBy).HasMaxLength(100);

        builder.HasIndex(lancamento => new { lancamento.TenantId, lancamento.Vencimento })
            .HasDatabaseName("ix_lancamentos_tenant_vencimento");
        // Totais por tipo/situação: valor no INCLUDE para somar em Index Only Scan.
        builder.HasIndex(lancamento => new { lancamento.TenantId, lancamento.Tipo, lancamento.Status })
            .IncludeProperties(lancamento => lancamento.Valor)
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_lancamentos_tenant_tipo_status_valor");
        builder.HasIndex(lancamento => new { lancamento.TenantId, lancamento.PagoEm })
            .HasDatabaseName("ix_lancamentos_tenant_pago_em");
    }
}
