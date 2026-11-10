using Advocacia.Modules.Drafting.Domain.Pecas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Configurations;

public sealed class RetrievedSourceConfiguration : IEntityTypeConfiguration<RetrievedSource>
{
    public void Configure(EntityTypeBuilder<RetrievedSource> builder)
    {
        builder.ToTable("retrieved_sources");

        builder.HasKey(source => source.Id);
        builder.Property(source => source.Id).ValueGeneratedNever();

        builder.Property(source => source.LegalDocumentRequestId)
            .HasConversion(id => id.Value, value => LegalDocumentRequestId.From(value));

        builder.Property(source => source.TenantId).IsRequired();
        builder.Property(source => source.QdrantPointId).HasMaxLength(64).IsRequired();
        builder.Property(source => source.NomeArquivo).HasMaxLength(RetrievedSource.FileNameMaxLength).IsRequired();
        builder.Property(source => source.Trecho).IsRequired();
        builder.Property(source => source.MetadadosJson).HasColumnType("jsonb");

        builder.HasIndex(source => source.LegalDocumentRequestId).HasDatabaseName("ix_retrieved_sources_request");
        builder.HasIndex(source => new { source.TenantId, source.DocumentoOrigemId })
            .HasDatabaseName("ix_retrieved_sources_tenant_documento");
    }
}
