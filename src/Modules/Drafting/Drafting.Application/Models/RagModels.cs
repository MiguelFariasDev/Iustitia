namespace Advocacia.Modules.Drafting.Application.Models;

/// <summary>Metadados de um trecho indexado (o payload do ponto no banco vetorial).</summary>
public sealed record ChunkMetadata(
    Guid? DocumentoOrigemId,
    string NomeArquivo,
    int? Pagina,
    string? TipoDocumento,
    string? Area,
    string? Tribunal,
    DateOnly? DataPublicacao,
    string? UrlOrigem,
    string HashConteudo);

/// <summary>Ponto a gravar no banco vetorial. <see cref="TenantId"/> é obrigatório e vira filtro de toda busca.</summary>
public sealed record VectorPoint(Guid TenantId, string Id, float[] Vector, string Texto, ChunkMetadata Meta);

public sealed record VectorHit(string PointId, string Texto, double Score, ChunkMetadata Meta);

/// <param name="TenantId">Sempre aplicado como filtro: o banco vetorial não tem RLS, então o isolamento entre escritórios é isto.</param>
public sealed record VectorSearchQuery(
    Guid TenantId,
    float[] Vector,
    int Limit,
    string? Area = null,
    string? TipoDocumento = null,
    DateOnly? PublicadoDe = null,
    DateOnly? PublicadoAte = null,
    IReadOnlyList<Guid>? DocumentosIds = null,
    double? ScoreMinimo = null);

public sealed record RetrievedChunk(string PointId, string Texto, double Score, ChunkMetadata Meta)
{
    /// <summary>Rótulo de citação: o que o modelo deve escrever entre colchetes. Fonte única de verdade.</summary>
    public string Rotulo => CitationLabel.For(Meta.NomeArquivo, Meta.Pagina);
}

public sealed record RagQuery(
    Guid TenantId,
    string TipoPeca,
    string Area,
    string Fatos,
    string Pedidos,
    IReadOnlyList<Guid> DocumentosIds);

public sealed record RagContext(IReadOnlyList<RetrievedChunk> Chunks, int TokensAproximados, int Descartados);

public static class CitationLabel
{
    /// <summary>"[" e "]" no nome quebrariam o formato [ARQUIVO, p. X]; viram parênteses.</summary>
    public static string For(string nomeArquivo, int? pagina)
    {
        var nome = nomeArquivo.Replace('[', '(').Replace(']', ')').Replace(',', ';').Trim();

        return $"{nome}, p. {(pagina is { } p ? p.ToString(System.Globalization.CultureInfo.InvariantCulture) : "s/n")}";
    }
}
