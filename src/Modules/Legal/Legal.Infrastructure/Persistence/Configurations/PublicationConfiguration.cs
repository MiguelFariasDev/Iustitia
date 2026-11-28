using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Configurations;

/// <summary>
/// Publication implementa IHasTenant e ISoftDeletable, então os filtros globais de tenant e
/// soft delete são aplicados centralmente por BaseDbContext — não redeclarar HasQueryFilter
/// aqui (ver PlatformDbContext/UserConfiguration).
///
/// Os índices declarados aqui são os que o EF Core consegue expressar (B-tree e parciais).
/// GIN (full-text e trigram) e BRIN não têm tradução em Fluent API e vivem em
/// infra/supabase/migrations/010_publications_indexes.sql — ver ADR-057.
/// </summary>
public sealed class PublicationConfiguration : IEntityTypeConfiguration<Publication>
{
    public void Configure(EntityTypeBuilder<Publication> builder)
    {
        builder.ToTable("publications");

        builder.HasKey(publication => publication.Id);
        builder.Property(publication => publication.Id)
            .HasConversion(id => id.Value, value => PublicationId.From(value))
            .ValueGeneratedNever();

        builder.Property(publication => publication.TenantId).IsRequired();

        builder.Property(publication => publication.ProcessId).IsRequired(false);

        builder.Property(publication => publication.ClienteId).IsRequired(false);
        builder.Property(publication => publication.ResponsavelUserId).IsRequired(false);

        // Colunas derivadas (ver Publication): planas e indexáveis, ao contrário do value object.
        builder.Property(publication => publication.Tribunal).HasMaxLength(12);
        builder.Property(publication => publication.CnjDigits).HasMaxLength(20).IsRequired();
        builder.Property(publication => publication.PublishedOn).HasColumnType("date").IsRequired();

        builder.Property(publication => publication.CnjNumber)
            // Persiste a forma mascarada (canônica): é o que o usuário busca e o que aparece
            // em qualquer exportação. Os dígitos puros são derivados no domínio quando
            // necessário (CNJNumber.Digits).
            .HasConversion(
                cnjNumber => cnjNumber.Value,
                value => CNJNumber.Create(value).Value)
            .HasColumnName("cnj_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(publication => publication.ExternalId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(publication => publication.Source)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(publication => publication.RawContent)
            // FromPersistence (e não Create) na volta: o valor no banco já está normalizado
            // e revalidar a cada materialização custaria regex em toda query, além de poder
            // lançar em cima de dado legado.
            .HasConversion(
                rawContent => rawContent.Value,
                value => RawContent.FromPersistence(value))
            .HasColumnName("raw_content")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(publication => publication.PublishedAt)
            .HasConversion(
                publishedAt => publishedAt.Value,
                value => PublishedAt.FromPersistence(value))
            .HasColumnName("published_at")
            .IsRequired();

        builder.Property(publication => publication.CapturedAt).IsRequired();

        builder.Property(publication => publication.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(publication => publication.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(publication => publication.IsRead)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(publication => publication.MetadataJson)
            .HasColumnType("jsonb");

        builder.Property(publication => publication.ReviewNotes).HasMaxLength(2000);

        builder.Property(publication => publication.CreatedAt).IsRequired();
        builder.Property(publication => publication.CreatedBy).HasMaxLength(100);
        builder.Property(publication => publication.UpdatedBy).HasMaxLength(100);
        builder.Property(publication => publication.DeletedBy).HasMaxLength(100);

        // Deduplicação (ver ADR-056). Source entra na chave porque o mesmo id numérico pode
        // ser reaproveitado por outro diário quando novas origens forem adicionadas.
        // IsUnique com filtro de soft delete: uma publicação excluída logicamente não deve
        // impedir a recaptura da mesma comunicação.
        builder.HasIndex(publication => new { publication.TenantId, publication.ExternalId, publication.Source })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ux_publications_tenant_external_id");

        builder.HasIndex(publication => new { publication.TenantId, publication.ProcessId, publication.PublishedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_publications_tenant_process_published");


        // Índices de LISTAGEM (medidos em 300 mil linhas, escritório com 150 mil — ver
        // docs/database/performance-consultas.md). Todos parciais em is_deleted = false (o filtro
        // global do EF sempre o aplica) e já na ordem da tela, o que elimina o Sort.
        //
        // Lista geral / não lidas / contagem total: published_on DESC, captured_at DESC.
        builder.HasIndex(publication => new { publication.TenantId, publication.PublishedOn, publication.CapturedAt })
            .IsDescending(false, true, true)
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_publications_tenant_published_cap");

        // Lista por situação (fila de revisão, arquivadas...) e contadores do painel. is_read
        // viaja no INCLUDE para os contadores saírem em Index Only Scan, sem visitar a tabela.
        builder.HasIndex(publication => new { publication.TenantId, publication.Status, publication.PublishedOn, publication.CapturedAt })
            .IsDescending(false, false, true, true)
            .IncludeProperties(publication => publication.IsRead)
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_publications_tenant_status_published");

        // Filtros por cliente e por responsável também ordenam por data: sem published_on no
        // índice o Postgres lê todas as linhas do responsável e ordena em memória.
        builder.HasIndex(publication => new { publication.TenantId, publication.ClienteId, publication.PublishedOn, publication.CapturedAt })
            .IsDescending(false, false, true, true)
            .HasFilter("is_deleted = false AND cliente_id IS NOT NULL")
            .HasDatabaseName("ix_publications_tenant_cliente_pub");

        builder.HasIndex(publication => new { publication.TenantId, publication.ResponsavelUserId, publication.PublishedOn, publication.CapturedAt })
            .IsDescending(false, false, true, true)
            .HasFilter("is_deleted = false AND responsavel_user_id IS NOT NULL")
            .HasDatabaseName("ix_publications_tenant_responsavel_pub");

        builder.HasIndex(publication => new { publication.TenantId, publication.ClienteId })
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_publications_tenant_cliente");

        builder.HasIndex(publication => new { publication.TenantId, publication.ResponsavelUserId })
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_publications_tenant_responsavel");

        builder.HasIndex(publication => new { publication.TenantId, publication.Tribunal, publication.PublishedOn })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_publications_tenant_tribunal_published_on");


    }
}
