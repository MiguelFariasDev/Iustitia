using Advocacia.Modules.Drafting.Domain.Pecas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Configurations;

public sealed class CitacaoPecaConfiguration : IEntityTypeConfiguration<CitacaoPeca>
{
    public void Configure(EntityTypeBuilder<CitacaoPeca> builder)
    {
        builder.ToTable("legal_document_citations");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.LegalDocumentRequestId).HasConversion(id => id.Value, value => LegalDocumentRequestId.From(value));

        // Pode conter fato do caso (valor, data, processo): cifrado.
        builder.Property(c => c.Texto).HasConversion(DraftingEncryption.Conversor(DraftingEncryption.Citacao)).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.Detalhe).HasMaxLength(CitacaoPeca.DetalheMaxLength).IsRequired();
        builder.Property(c => c.PontoId).HasMaxLength(64);
        builder.Property(c => c.FonteUrl).HasMaxLength(1000);
        builder.Property(c => c.Urn).HasMaxLength(300);
        builder.Property(c => c.Decisao).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Justificativa).HasMaxLength(CitacaoPeca.JustificativaMaxLength);

        builder.HasIndex(c => new { c.LegalDocumentRequestId, c.Ordem }).HasDatabaseName("ix_legal_document_citations_request");
    }
}
