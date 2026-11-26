using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Domain.Seguranca;
using Advocacia.BuildingBlocks.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Configurations;

/// <summary>
/// Contextos de criptografia do módulo. São dado autenticado (AAD): mudar uma destas strings torna ilegível o
/// que já foi gravado.
/// </summary>
internal static class DraftingEncryption
{
    public const string Fatos = "drafting.peca.fatos";
    public const string Pedidos = "drafting.peca.pedidos";
    public const string Instrucoes = "drafting.peca.instrucoes";
    public const string Texto = "drafting.peca.texto";
    public const string TextoRevisado = "drafting.peca.texto_revisado";
    public const string Consulta = "drafting.peca.consulta";
    public const string Trecho = "drafting.fonte.trecho";
    public const string ChunkCaso = "drafting.caso.chunk";
    public const string Classificacao = "drafting.caso.classificacao";
    public const string ClienteNome = "drafting.caso.cliente_nome";
    public const string Citacao = "drafting.citacao.texto";
    public const string Avisos = "drafting.peca.avisos";

    public static ValueConverter Conversor(string contexto) => new EncryptedStringConverter(contexto);
}

public sealed class CasoConfiguration : IEntityTypeConfiguration<Caso>
{
    public void Configure(EntityTypeBuilder<Caso> builder)
    {
        builder.ToTable("casos");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, value => CasoId.From(value)).ValueGeneratedNever();

        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.Titulo).HasMaxLength(Caso.TituloMaxLength).IsRequired();
        builder.Property(c => c.Area).HasMaxLength(60).IsRequired();
        builder.Property(c => c.ClienteNome).HasConversion(DraftingEncryption.Conversor(DraftingEncryption.ClienteNome));
        builder.Property(c => c.NumeroProcesso).HasMaxLength(Caso.NumeroProcessoMaxLength);
        builder.Property(c => c.Uf).HasMaxLength(2);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(100);
        builder.Property(c => c.UpdatedBy).HasMaxLength(100);

        builder.HasMany(c => c.Membros).WithOne().HasForeignKey(m => m.CasoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Membros).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => new { c.TenantId, c.UsuarioIdResponsavel, c.CriadoEm }).HasDatabaseName("ix_casos_tenant_responsavel");
        builder.HasIndex(c => new { c.TenantId, c.ClienteId }).HasDatabaseName("ix_casos_tenant_cliente");
    }
}

public sealed class CasoMembroConfiguration : IEntityTypeConfiguration<CasoMembro>
{
    public void Configure(EntityTypeBuilder<CasoMembro> builder)
    {
        builder.ToTable("caso_membros");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.CasoId).HasConversion(id => id.Value, value => CasoId.From(value));
        builder.Property(m => m.Permissao).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(m => new { m.CasoId, m.UsuarioId }).IsUnique().HasDatabaseName("ux_caso_membros_caso_usuario");
        builder.HasIndex(m => new { m.TenantId, m.UsuarioId }).HasDatabaseName("ix_caso_membros_tenant_usuario");
    }
}

public sealed class CasoDocumentoConfiguration : IEntityTypeConfiguration<CasoDocumento>
{
    public void Configure(EntityTypeBuilder<CasoDocumento> builder)
    {
        builder.ToTable("caso_documentos");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasConversion(id => id.Value, value => CasoDocumentoId.From(value)).ValueGeneratedNever();
        builder.Property(d => d.CasoId).HasConversion(id => id.Value, value => CasoId.From(value));

        builder.Property(d => d.NomeArquivo).HasMaxLength(CasoDocumento.NomeMaxLength).IsRequired();
        builder.Property(d => d.Formato).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.HashSha256).HasMaxLength(64).IsRequired();
        builder.Property(d => d.ChaveOriginal).HasMaxLength(300).IsRequired();
        builder.Property(d => d.ChaveMarkdown).HasMaxLength(300);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.Erro).HasMaxLength(CasoDocumento.ErroMaxLength);
        builder.Property(d => d.Origem).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.TipoDocumento).HasMaxLength(60);
        builder.Property(d => d.Area).HasMaxLength(60);
        builder.Property(d => d.ClassificacaoJson).HasConversion(DraftingEncryption.Conversor(DraftingEncryption.Classificacao));
        builder.Property(d => d.CreatedBy).HasMaxLength(100);
        builder.Property(d => d.UpdatedBy).HasMaxLength(100);

        builder.HasOne<Caso>().WithMany().HasForeignKey(d => d.CasoId).OnDelete(DeleteBehavior.Restrict);

        // O mesmo arquivo não entra duas vezes no mesmo CASO; em outro caso, entra (isolamento).
        builder.HasIndex(d => new { d.CasoId, d.HashSha256 }).IsUnique().HasDatabaseName("ux_caso_documentos_caso_hash");
        builder.HasIndex(d => new { d.TenantId, d.CasoId, d.Status }).HasDatabaseName("ix_caso_documentos_tenant_caso_status");
    }
}

public sealed class CasoDocumentoChunkConfiguration : IEntityTypeConfiguration<CasoDocumentoChunk>
{
    public void Configure(EntityTypeBuilder<CasoDocumentoChunk> builder)
    {
        builder.ToTable("caso_documento_chunks");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.CasoId).HasConversion(id => id.Value, value => CasoId.From(value));
        builder.Property(c => c.DocumentoId).HasConversion(id => id.Value, value => CasoDocumentoId.From(value));

        builder.Property(c => c.Secao).HasMaxLength(300);
        builder.Property(c => c.Texto).HasConversion(DraftingEncryption.Conversor(DraftingEncryption.ChunkCaso)).IsRequired();
        builder.Property(c => c.HashSha256).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Origem).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne<CasoDocumento>().WithMany().HasForeignKey(c => c.DocumentoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.DocumentoId, c.Ordem }).IsUnique().HasDatabaseName("ux_caso_documento_chunks_doc_ordem");
        builder.HasIndex(c => new { c.TenantId, c.CasoId }).HasDatabaseName("ix_caso_documento_chunks_tenant_caso");
    }
}

public sealed class EventoSegurancaIaConfiguration : IEntityTypeConfiguration<EventoSegurancaIa>
{
    public void Configure(EntityTypeBuilder<EventoSegurancaIa> builder)
    {
        builder.ToTable("drafting_security_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Tipo).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Origem).HasMaxLength(200).IsRequired();
        builder.Property(e => e.HashConteudo).HasMaxLength(64);

        builder.HasIndex(e => new { e.TenantId, e.OcorridoEm }).HasDatabaseName("ix_drafting_security_events_tenant_data");
    }
}
