using Advocacia.Modules.Drafting.Domain.Pecas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Configurations;

public sealed class LegalDocumentRequestConfiguration : IEntityTypeConfiguration<LegalDocumentRequest>
{
    public void Configure(EntityTypeBuilder<LegalDocumentRequest> builder)
    {
        builder.ToTable("legal_document_requests");

        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id)
            .HasConversion(id => id.Value, value => LegalDocumentRequestId.From(value))
            .ValueGeneratedNever();

        builder.Property(request => request.RequestOrigemId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? LegalDocumentRequestId.From(value.Value) : (LegalDocumentRequestId?)null);

        builder.Property(request => request.TenantId).IsRequired();
        builder.Property(request => request.UsuarioId).IsRequired();
        builder.Property(request => request.TipoPeca).HasMaxLength(LegalDocumentRequest.TipoMaxLength).IsRequired();
        builder.Property(request => request.Area).HasMaxLength(LegalDocumentRequest.AreaMaxLength).IsRequired();
        // Dado sigiloso do caso: cifrado em repouso (linhas antigas em texto puro continuam legíveis, ver ADR-068).
        builder.Property(request => request.Fatos).HasConversion(DraftingEncryption.Conversor(DraftingEncryption.Fatos)).IsRequired();
        builder.Property(request => request.Pedidos).HasConversion(DraftingEncryption.Conversor(DraftingEncryption.Pedidos)).IsRequired();
        builder.Property(request => request.InstrucoesAdicionais).HasConversion(DraftingEncryption.Conversor(DraftingEncryption.Instrucoes));
        builder.Property(request => request.TextoGerado).HasConversion(DraftingEncryption.Conversor(DraftingEncryption.Texto));
        builder.Property(request => request.TextoRevisado).HasConversion(DraftingEncryption.Conversor(DraftingEncryption.TextoRevisado));
        builder.Property(request => request.ConsultaJson).HasConversion(DraftingEncryption.Conversor(DraftingEncryption.Consulta));
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(request => request.EtapaAtual).HasMaxLength(LegalDocumentRequest.EtapaMaxLength);
        builder.Property(request => request.Progresso).IsRequired();
        builder.Property(request => request.Tentativas).IsRequired();
        builder.Property(request => request.CriadoEm).IsRequired();
        builder.Property(request => request.CustoEstimadoUsd).HasPrecision(12, 6);
        // Os avisos podem repetir fatos do caso: texto cifrado (era jsonb, que não aceita cifra).
        builder.Property(request => request.AvisosJson).HasColumnType("text")
            .HasConversion(DraftingEncryption.Conversor(DraftingEncryption.Avisos));
        builder.Property(request => request.ModeloUtilizado).HasMaxLength(LegalDocumentRequest.ModeloMaxLength);
        builder.Property(request => request.Erro).HasMaxLength(LegalDocumentRequest.ErroMaxLength);
        builder.Property(request => request.Versao).IsRequired();
        builder.Property(request => request.PromptVersao).HasMaxLength(60);
        builder.Property(request => request.DocumentosIdsJson).HasColumnType("jsonb");
        builder.Property(request => request.CreatedBy).HasMaxLength(100);
        builder.Property(request => request.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(request => new { request.TenantId, request.Status, request.CriadoEm })
            .HasDatabaseName("ix_legal_document_requests_tenant_status");

        builder.HasMany(request => request.Sources)
            .WithOne()
            .HasForeignKey(source => source.LegalDocumentRequestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(request => request.Sources).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(request => request.Events)
            .WithOne()
            .HasForeignKey(evento => evento.LegalDocumentRequestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(request => request.Events).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(request => request.Citacoes)
            .WithOne()
            .HasForeignKey(citacao => citacao.LegalDocumentRequestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(request => request.Citacoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(request => new { request.TenantId, request.CasoId })
            .HasDatabaseName("ix_legal_document_requests_tenant_caso");

        // Histórico do usuário e cadeia de versões.
        builder.HasIndex(request => new { request.TenantId, request.UsuarioId, request.CriadoEm })
            .HasDatabaseName("ix_legal_document_requests_tenant_user_created");
        builder.HasIndex(request => request.RequestOrigemId)
            .HasFilter("request_origem_id IS NOT NULL")
            .HasDatabaseName("ix_legal_document_requests_origem");
    }
}
