using Advocacia.Modules.Drafting.Domain.Normas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Configurations;

/// <summary>
/// Acervo normativo PÚBLICO: compartilhado entre escritórios, sem tenant e sem RLS (nunca guarda dado de
/// cliente). A coluna <c>tsv</c> (full-text) e seu índice GIN são criados na migração SQL, não aqui.
/// </summary>
public sealed class NormaJuridicaConfiguration : IEntityTypeConfiguration<NormaJuridica>
{
    public void Configure(EntityTypeBuilder<NormaJuridica> builder)
    {
        builder.ToTable("normas_juridicas");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.RepoPath).HasMaxLength(600).IsRequired();
        builder.Property(n => n.Urn).HasMaxLength(300);
        builder.Property(n => n.Titulo).HasMaxLength(500).IsRequired();
        builder.Property(n => n.Tipo).HasMaxLength(120).IsRequired();
        builder.Property(n => n.Numero).HasMaxLength(40);
        builder.Property(n => n.DataPublicacao).HasMaxLength(120);
        builder.Property(n => n.Ementa).HasMaxLength(4000);
        builder.Property(n => n.Situacao).HasMaxLength(200).IsRequired();
        builder.Property(n => n.VersaoTexto).HasMaxLength(500).IsRequired();
        builder.Property(n => n.Esfera).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Uf).HasMaxLength(2);
        builder.Property(n => n.Municipio).HasMaxLength(120);
        builder.Property(n => n.Tribunal).HasMaxLength(40);
        builder.Property(n => n.Area).HasMaxLength(40);
        builder.Property(n => n.Colecao).HasMaxLength(40).IsRequired();
        builder.Property(n => n.FonteOficialUrl).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.ArquivoMarkdownPath).HasMaxLength(700).IsRequired();
        builder.Property(n => n.HashSha256).HasMaxLength(64).IsRequired();
        builder.Property(n => n.Origem).HasMaxLength(80).IsRequired();
        builder.Property(n => n.Erro).HasMaxLength(500);
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.Assuntos).HasColumnType("text[]");

        builder.HasIndex(n => n.RepoPath).IsUnique().HasDatabaseName("ux_normas_juridicas_repo_path");
        builder.HasIndex(n => n.Urn).HasDatabaseName("ix_normas_juridicas_urn");
        builder.HasIndex(n => new { n.Colecao, n.Status }).HasDatabaseName("ix_normas_juridicas_colecao_status");
        builder.HasIndex(n => new { n.Esfera, n.Uf, n.Municipio }).HasDatabaseName("ix_normas_juridicas_esfera_uf_municipio");
        builder.HasIndex(n => new { n.Tipo, n.Numero, n.Ano }).HasDatabaseName("ix_normas_juridicas_tipo_numero_ano");
    }
}

public sealed class NormaChunkConfiguration : IEntityTypeConfiguration<NormaChunk>
{
    public void Configure(EntityTypeBuilder<NormaChunk> builder)
    {
        builder.ToTable("norma_chunks");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Texto).IsRequired();
        builder.Property(c => c.Secao).HasMaxLength(500);
        builder.Property(c => c.Artigo).HasMaxLength(40);
        builder.Property(c => c.Paragrafo).HasMaxLength(60);
        builder.Property(c => c.Inciso).HasMaxLength(20);
        builder.Property(c => c.HashSha256).HasMaxLength(64).IsRequired();
        builder.Property(c => c.QdrantPointId).HasMaxLength(40);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne<NormaJuridica>().WithMany().HasForeignKey(c => c.NormaJuridicaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.NormaJuridicaId, c.Versao, c.Ordem }).IsUnique().HasDatabaseName("ux_norma_chunks_norma_versao_ordem");
        builder.HasIndex(c => c.HashSha256).HasDatabaseName("ix_norma_chunks_hash");
    }
}

public sealed class NormaVersaoConfiguration : IEntityTypeConfiguration<NormaVersao>
{
    public void Configure(EntityTypeBuilder<NormaVersao> builder)
    {
        builder.ToTable("norma_versoes");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.VersaoTexto).HasMaxLength(500).IsRequired();
        builder.Property(v => v.Situacao).HasMaxLength(200).IsRequired();
        builder.Property(v => v.HashSha256).HasMaxLength(64).IsRequired();
        builder.Property(v => v.ArquivoMarkdownPath).HasMaxLength(700).IsRequired();
        builder.Property(v => v.CommitOrigem).HasMaxLength(64);

        builder.HasOne<NormaJuridica>().WithMany().HasForeignKey(v => v.NormaJuridicaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(v => new { v.NormaJuridicaId, v.Numero }).IsUnique().HasDatabaseName("ux_norma_versoes_norma_numero");
    }
}

public sealed class IngestaoJobConfiguration : IEntityTypeConfiguration<IngestaoJob>
{
    public void Configure(EntityTypeBuilder<IngestaoJob> builder)
    {
        builder.ToTable("ingestao_jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();

        builder.Property(j => j.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(j => j.CommitOrigem).HasMaxLength(64);
        builder.Property(j => j.Escopo).HasMaxLength(500);
        builder.Property(j => j.Erro).HasMaxLength(500);

        builder.HasIndex(j => new { j.Status, j.IniciadoEm }).HasDatabaseName("ix_ingestao_jobs_status_iniciado");
    }
}
