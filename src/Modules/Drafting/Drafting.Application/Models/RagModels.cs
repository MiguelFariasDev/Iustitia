using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Domain.Casos;

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
    string HashConteudo,
    IReadOnlyList<string>? Tags = null,
    NormaRef? Norma = null,
    CasoRef? Caso = null);

/// <summary>
/// Presente só em trechos do acervo normativo público (leis, jurisprudência…). Carrega o necessário para
/// citar com rastreabilidade: qual norma, qual dispositivo, qual versão, qual arquivo Markdown e qual a
/// fonte oficial. Trechos de anexos não têm isto.
/// </summary>
public sealed record NormaRef(
    Guid NormaId,
    string Titulo,
    string Tipo,
    string? Numero,
    string? Urn,
    string Situacao,
    bool Revogada,
    string VersaoTexto,
    string Esfera,
    string? Uf,
    string? Municipio,
    string Colecao,
    string ArquivoMarkdownPath,
    string? Artigo,
    string? Paragrafo,
    string? Inciso,
    string? Hierarquia,
    int VersaoNumero);

/// <summary>
/// Presente só em trechos de anexos. É o lastro do ISOLAMENTO: sem isto um trecho não pode ser usado, e o
/// <see cref="CasoId"/> é conferido de novo depois da busca, além do filtro obrigatório no banco vetorial.
/// </summary>
public sealed record CasoRef(
    Guid TenantId,
    Guid CasoId,
    Guid ClienteId,
    Guid DocumentoId,
    Guid UsuarioIdResponsavel,
    OrigemTexto Origem,
    double? Confianca,
    string? Secao,
    bool Restrito,
    bool Suspeito,
    bool RevisaoPendente = false);

/// <summary>Trecho encontrado por uma das buscas. A escala do score depende da busca: compare só dentro da mesma lista.</summary>
public sealed record VectorHit(string PointId, string Texto, double Score, ChunkMetadata Meta);

public sealed record RetrievedChunk(string PointId, string Texto, double Score, ChunkMetadata Meta)
{
    /// <summary>Rótulo de citação: o que o modelo deve escrever entre colchetes. Fonte única de verdade.</summary>
    public string Rotulo => CitationLabel.For(Meta);
}

/// <summary>Grupo de contexto mostrado ao modelo. A separação é exigência do produto: fato não se mistura com norma.</summary>
public enum GrupoContexto
{
    DocumentosDoCaso = 0,
    Constituicao = 1,
    LeisFederais = 2,
    Jurisprudencia = 3,
}

/// <param name="Chunks">5 a 10 trechos, já na ordem de apresentação (grupo, depois relevância).</param>
/// <param name="Cortados">Trechos relevantes que não couberam na janela do modelo.</param>
public sealed record RagContext(
    IReadOnlyList<RetrievedChunk> Chunks, int TokensAproximados, int Descartados, int Cortados = 0)
{
    public static GrupoContexto GrupoDe(ChunkMetadata meta) => meta.Norma switch
    {
        null => GrupoContexto.DocumentosDoCaso,
        { Colecao: Normas.Colecoes.ConstituicaoFederal } => GrupoContexto.Constituicao,
        { Colecao: Normas.Colecoes.Jurisprudencia } => GrupoContexto.Jurisprudencia,
        _ => GrupoContexto.LeisFederais,
    };

    public IEnumerable<RetrievedChunk> DoGrupo(GrupoContexto grupo) => Chunks.Where(c => GrupoDe(c.Meta) == grupo);
}

public static class CitationLabel
{
    public static string For(ChunkMetadata meta) =>
        meta.Norma is { } norma ? For(norma, meta) : For(meta.NomeArquivo, meta.Pagina);

    /// <summary>
    /// Lei: "Código Civil, art. 186". Jurisprudência: "STJ, Súmula nº 297" (com página quando existe).
    /// Colchetes e vírgulas do nome quebrariam o formato; viram parênteses e ponto e vírgula.
    /// </summary>
    private static string For(NormaRef norma, ChunkMetadata meta)
    {
        if (norma.Colecao == Normas.Colecoes.Jurisprudencia)
        {
            var tribunal = meta.Tribunal?.ToUpperInvariant();
            var titulo = Limpar(norma.Titulo);
            if (tribunal is not null)
            {
                foreach (var sufixo in new[] { $" do {tribunal}", $" da {tribunal}" })
                {
                    if (titulo.EndsWith(sufixo, StringComparison.OrdinalIgnoreCase))
                    {
                        titulo = titulo[..^sufixo.Length];
                    }
                }
            }

            var rotulo = tribunal is null ? titulo : $"{tribunal}, {titulo}";

            return meta.Pagina is { } pagina ? $"{rotulo}, p. {pagina}" : rotulo;
        }

        var conhecida = NormasConhecidas.Identificar(norma.Tipo, norma.Numero, meta.DataPublicacao?.Year, norma.Colecao);
        var nome = conhecida?.Apelido ?? Limpar(norma.Titulo);

        // ADCT e anexos reiniciam a numeração: "Constituição Federal, art. 5º" e o art. 5º do ADCT são coisas diferentes.
        if (norma.Hierarquia is { } hierarquia && hierarquia.StartsWith("ATO DAS", StringComparison.OrdinalIgnoreCase))
        {
            nome += " (ADCT)";
        }

        return string.IsNullOrWhiteSpace(norma.Artigo) ? $"{nome}, texto" : $"{nome}, art. {norma.Artigo.Trim()}";
    }

    public static string For(string nomeArquivo, int? pagina) =>
        $"{Limpar(nomeArquivo)}, p. {(pagina is { } p ? p.ToString(System.Globalization.CultureInfo.InvariantCulture) : "s/n")}";

    private static string Limpar(string nome) => nome.Replace('[', '(').Replace(']', ')').Replace(',', ';').Trim();
}
