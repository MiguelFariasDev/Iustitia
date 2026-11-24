using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Drafting.Domain.Normas;

/// <summary>
/// Uma norma (ou decisão) do acervo público. É conhecimento COMPARTILHADO entre os escritórios: não tem
/// tenant nem RLS e nunca contém dado de cliente. A identidade operacional é <see cref="RepoPath"/>
/// (caminho no repositório de origem); a <see cref="Urn"/> nem sempre existe no YAML.
/// </summary>
public sealed class NormaJuridica : Entity<Guid>
{
    public string RepoPath { get; private set; } = string.Empty;

    public string? Urn { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public string Tipo { get; private set; } = string.Empty;

    public string? Numero { get; private set; }

    public int? Ano { get; private set; }

    public DateOnly? Data { get; private set; }

    public string? DataPublicacao { get; private set; }

    public string? Ementa { get; private set; }

    public string Situacao { get; private set; } = string.Empty;

    public bool Revogada { get; private set; }

    public string VersaoTexto { get; private set; } = string.Empty;

    public string Esfera { get; private set; } = string.Empty;

    public string? Uf { get; private set; }

    public string? Municipio { get; private set; }

    public string? Tribunal { get; private set; }

    public string? Area { get; private set; }

    public string[] Assuntos { get; private set; } = [];

    public string Colecao { get; private set; } = string.Empty;

    public string FonteOficialUrl { get; private set; } = string.Empty;

    /// <summary>Chave do Markdown atual no object storage.</summary>
    public string ArquivoMarkdownPath { get; private set; } = string.Empty;

    public string HashSha256 { get; private set; } = string.Empty;

    /// <summary>Número da versão do texto atual (1, 2, …) — casa com <see cref="NormaVersao.Numero"/>.</summary>
    public int VersaoAtual { get; private set; }

    public string Origem { get; private set; } = string.Empty;

    public DateTimeOffset DataIngestao { get; private set; }

    public DateTimeOffset DataUltimaAtualizacao { get; private set; }

    public NormaStatus Status { get; private set; }

    public string? Erro { get; private set; }

    private NormaJuridica()
    {
        // EF Core.
    }

    public static NormaJuridica Criar(
        NormaMetadata metadata, string repoPath, string arquivoMarkdownPath, string hash, string origem, DateTimeOffset agora)
    {
        var norma = new NormaJuridica
        {
            Id = Guid.NewGuid(),
            RepoPath = repoPath,
            Origem = origem,
            DataIngestao = agora,
            VersaoAtual = 1,
        };
        norma.Aplicar(metadata, arquivoMarkdownPath, hash, agora);

        return norma;
    }

    /// <summary>O conteúdo mudou: novos metadados, nova versão, de volta para "Pendente" até reindexar.</summary>
    public void Atualizar(NormaMetadata metadata, string arquivoMarkdownPath, string hash, DateTimeOffset agora)
    {
        VersaoAtual++;
        Aplicar(metadata, arquivoMarkdownPath, hash, agora);
    }

    /// <summary>Mesmo conteúdo, indexação a refazer (falha anterior): não cria versão nova.</summary>
    public void ReabrirParaIndexacao(NormaMetadata metadata, string arquivoMarkdownPath, string hash, DateTimeOffset agora) =>
        Aplicar(metadata, arquivoMarkdownPath, hash, agora);

    public void MarcarIndexada(DateTimeOffset agora)
    {
        Status = NormaStatus.Indexada;
        Erro = null;
        DataUltimaAtualizacao = agora;
    }

    public void MarcarFalha(string erro, DateTimeOffset agora)
    {
        Status = NormaStatus.Falhou;
        Erro = erro.Length > 500 ? erro[..500] : erro;
        DataUltimaAtualizacao = agora;
    }

    public void MarcarRemovida(DateTimeOffset agora)
    {
        Status = NormaStatus.Removida;
        DataUltimaAtualizacao = agora;
    }

    private void Aplicar(NormaMetadata metadata, string arquivoMarkdownPath, string hash, DateTimeOffset agora)
    {
        Urn = metadata.Urn;
        Titulo = metadata.Titulo;
        Tipo = metadata.Tipo;
        Numero = metadata.Numero;
        Ano = metadata.Ano;
        Data = metadata.Data;
        DataPublicacao = metadata.DataPublicacao;
        Ementa = metadata.Ementa;
        Situacao = metadata.Situacao;
        Revogada = metadata.Revogada;
        VersaoTexto = metadata.VersaoTexto;
        Esfera = metadata.Esfera;
        Uf = metadata.Uf;
        Municipio = metadata.Municipio;
        Tribunal = metadata.Tribunal;
        Area = metadata.Area;
        Assuntos = [.. metadata.Assuntos];
        Colecao = metadata.Colecao;
        FonteOficialUrl = metadata.FonteOficialUrl;
        ArquivoMarkdownPath = arquivoMarkdownPath;
        HashSha256 = hash;
        Status = NormaStatus.Pendente;
        Erro = null;
        DataUltimaAtualizacao = agora;
    }
}
