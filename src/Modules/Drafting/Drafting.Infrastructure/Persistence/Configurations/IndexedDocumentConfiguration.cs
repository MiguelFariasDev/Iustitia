using Advocacia.Modules.Drafting.Domain.Acervo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Configurations;

public sealed class IndexedDocumentConfiguration : IEntityTypeConfiguration<IndexedDocument>
{
    public void Configure(EntityTypeBuilder<IndexedDocument> builder)
    {
        builder.ToTable("indexed_documents");

        builder.HasKey(document => document.Id);
        builder.Property(document => document.Id)
            .HasConversion(id => id.Value, value => IndexedDocumentId.From(value))
            .ValueGeneratedNever();

        builder.Property(document => document.TenantId).IsRequired();
        builder.Property(document => document.UsuarioId).IsRequired();
        builder.Property(document => document.NomeArquivo).HasMaxLength(IndexedDocument.NomeMaxLength).IsRequired();
        builder.Property(document => document.TipoDocumento).HasMaxLength(30).IsRequired();
        builder.Property(document => document.Area).HasMaxLength(60);
        builder.Property(document => document.Tribunal).HasMaxLength(20);
        builder.Property(document => document.UrlOrigem).HasMaxLength(1000);
        builder.Property(document => document.TagsJson).HasColumnType("jsonb");
        builder.Property(document => document.Visibilidade).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(document => document.HashConteudo).HasMaxLength(64).IsRequired();
        builder.Property(document => document.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(document => document.Erro).HasMaxLength(IndexedDocument.ErroMaxLength);
        builder.Property(document => document.PaginasJson).HasColumnType("jsonb");
        builder.Property(document => document.CreatedBy).HasMaxLength(100);
        builder.Property(document => document.UpdatedBy).HasMaxLength(100);

        // O mesmo conteúdo não entra duas vezes no mesmo escritório.
        builder.HasIndex(document => new { document.TenantId, document.HashConteudo })
            .IsUnique()
            .HasDatabaseName("ux_indexed_documents_tenant_hash");
        builder.HasIndex(document => new { document.TenantId, document.CriadoEm })
            .HasDatabaseName("ix_indexed_documents_tenant_created");
    }
}
