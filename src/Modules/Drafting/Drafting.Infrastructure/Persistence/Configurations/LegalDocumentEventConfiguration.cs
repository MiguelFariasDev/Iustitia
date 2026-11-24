using Advocacia.Modules.Drafting.Domain.Pecas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Configurations;

public sealed class LegalDocumentEventConfiguration : IEntityTypeConfiguration<LegalDocumentEvent>
{
    public void Configure(EntityTypeBuilder<LegalDocumentEvent> builder)
    {
        builder.ToTable("legal_document_events");

        builder.HasKey(evento => evento.Id);
        builder.Property(evento => evento.Id).ValueGeneratedNever();

        builder.Property(evento => evento.LegalDocumentRequestId)
            .HasConversion(id => id.Value, value => LegalDocumentRequestId.From(value));

        builder.Property(evento => evento.TenantId).IsRequired();
        builder.Property(evento => evento.Tipo).HasMaxLength(40).IsRequired();
        builder.Property(evento => evento.OcorridoEm).IsRequired();
        builder.Property(evento => evento.DetalheJson).HasColumnType("jsonb");

        builder.HasIndex(evento => new { evento.LegalDocumentRequestId, evento.OcorridoEm })
            .HasDatabaseName("ix_legal_document_events_request_time");
    }
}
