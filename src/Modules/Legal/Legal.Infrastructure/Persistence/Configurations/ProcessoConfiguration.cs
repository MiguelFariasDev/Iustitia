using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Configurations;

/// <summary>
/// Processo implementa IHasTenant e ISoftDeletable — filtros globais vêm do BaseDbContext.
///
/// NÃO há foreign key para clientes nem para users: o cliente é outro agregado (regra de DDD
/// tático — a consistência entre agregados é da aplicação, não do banco) e o usuário pertence
/// ao módulo Platform, cuja tabela este módulo não deve amarrar. A existência do cliente é
/// verificada no handler, antes de persistir.
/// </summary>
public sealed class ProcessoConfiguration : IEntityTypeConfiguration<Processo>
{
    public void Configure(EntityTypeBuilder<Processo> builder)
    {
        builder.ToTable("processos");

        builder.HasKey(processo => processo.Id);
        builder.Property(processo => processo.Id)
            .HasConversion(id => id.Value, value => ProcessoId.From(value))
            .ValueGeneratedNever();

        builder.Property(processo => processo.TenantId).IsRequired();

        // Persiste a forma mascarada (canônica), igual a publications.cnj_number — é o que
        // permite comparar processo e publicação sem normalizar dos dois lados.
        builder.Property(processo => processo.CnjNumber)
            .HasConversion(cnjNumber => cnjNumber.Value, value => CNJNumber.Create(value).Value)
            .HasColumnName("cnj_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(processo => processo.CnjDigits)
            .HasColumnName("cnj_digits")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(processo => processo.ClienteId).IsRequired();
        builder.Property(processo => processo.ResponsavelUserId).IsRequired();

        builder.Property(processo => processo.Area)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(processo => processo.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.OwnsOne(processo => processo.Foro, foro =>
        {
            foro.Property(f => f.Tribunal).HasColumnName("tribunal").HasMaxLength(20);
            foro.Property(f => f.Comarca).HasColumnName("comarca").HasMaxLength(120);
            foro.Property(f => f.Vara).HasColumnName("vara").HasMaxLength(160);
        });

        builder.Property(processo => processo.Assunto).HasMaxLength(300);
        builder.Property(processo => processo.ParteContraria).HasMaxLength(200);

        // numeric(18,2): valor de causa é dinheiro. double perderia centavos em somatórios e
        // um relatório financeiro errado é pior que um campo ausente.
        builder.Property(processo => processo.ValorCausa).HasColumnType("numeric(18,2)");

        builder.Property(processo => processo.DataDistribuicao);
        builder.Property(processo => processo.Observacoes).HasMaxLength(2000);

        builder.Property(processo => processo.CreatedAt).IsRequired();
        builder.Property(processo => processo.CreatedBy).HasMaxLength(100);
        builder.Property(processo => processo.UpdatedBy).HasMaxLength(100);
        builder.Property(processo => processo.DeletedBy).HasMaxLength(100);

        // Um número CNJ por escritório: o mesmo processo cadastrado duas vezes produziria
        // captura duplicada e duas timelines para o mesmo caso.
        builder.HasIndex(processo => new { processo.TenantId, processo.CnjNumber })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ux_processos_tenant_cnj");

        // Busca por número parcial consulta os dígitos, não a máscara.
        builder.HasIndex(processo => new { processo.TenantId, processo.CnjDigits })
            .HasDatabaseName("ix_processos_tenant_cnj_digits");

        builder.HasIndex(processo => new { processo.TenantId, processo.ClienteId })
            .HasDatabaseName("ix_processos_tenant_cliente");

        builder.HasIndex(processo => new { processo.TenantId, processo.ResponsavelUserId })
            .HasDatabaseName("ix_processos_tenant_responsavel");

        builder.HasIndex(processo => new { processo.TenantId, processo.Area, processo.Status })
            .HasDatabaseName("ix_processos_tenant_area_status");

        // Índice PARCIAL para a consulta mais crítica do sistema: quais processos o job deve
        // consultar no DJEN. Roda a cada 6 horas por tenant e só enxerga ativos/suspensos —
        // indexar só essas linhas mantém o índice pequeno mesmo com o histórico crescendo.
        builder.HasIndex(processo => new { processo.TenantId, processo.Status })
            .HasFilter("status IN ('Ativo', 'Suspenso') AND is_deleted = false")
            .HasDatabaseName("ix_processos_tenant_captura");
    }
}
