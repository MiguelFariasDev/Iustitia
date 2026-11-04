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

        builder.HasIndex(publication => new { publication.TenantId, publication.Status })
            .HasDatabaseName("ix_publications_tenant_status");

        // Fila de revisão humana. A segunda coluna é published_at, NÃO process_id: a consulta
        // da fila é "as pendentes do escritório, mais antigas primeiro"
        // (PublicationsPendingReviewSpecification), e um índice chaveado por process_id
        // obriga o Postgres a ler TODAS as pendentes e ordenar em memória.
        //
        // Medido na prova de conceito com 50 mil publicações (Etapa 1.3, Parte 6.3):
        // 25,0 ms com process_id (top-N heapsort sobre 2.500 linhas) contra 0,4 ms com
        // published_at (index scan já ordenado, lê só as 20 do LIMIT) — 60x.
        builder.HasIndex(publication => new { publication.TenantId, publication.PublishedAt })
            .HasFilter("status = 'PendingReview' AND is_deleted = false")
            .HasDatabaseName("ix_publications_tenant_pending_review");

        builder.HasIndex(publication => new { publication.TenantId, publication.IsRead })
            .HasFilter("is_read = false AND is_deleted = false")
            .HasDatabaseName("ix_publications_tenant_unread");

        builder.HasIndex(publication => new { publication.TenantId, publication.ClienteId })
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_publications_tenant_cliente");

        builder.HasIndex(publication => new { publication.TenantId, publication.ResponsavelUserId })
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_publications_tenant_responsavel");

        builder.HasIndex(publication => new { publication.TenantId, publication.Tribunal, publication.PublishedOn })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_publications_tenant_tribunal_published_on");

        builder.HasIndex(publication => new { publication.TenantId, publication.PublishedOn })
            .IsDescending(false, true)
            .HasDatabaseName("ix_publications_tenant_published_on");

        builder.HasIndex(publication => publication.CnjNumber)
            .HasDatabaseName("ix_publications_cnj_number");
    }
}
