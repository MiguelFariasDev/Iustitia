using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Domain.Casos;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Recuperação: consulta estruturada → consultas distintas (semântica dos fatos, semântica da tese, BM25 com
/// termos exatos, referências a dispositivos) → anexos do caso e base jurídica → contexto dentro da janela.
/// </summary>
public sealed class RagService(
    IConsultaEnriquecidaService consultas,
    IEmbeddingService embeddings,
    IBuscaDocumentosCasoService buscaCaso,
    IBuscaJuridicaService buscaJuridica,
    IOptions<RagOptions> rag,
    IOptions<BuscaNormasOptions> busca,
    IOptions<JanelaModeloOptions> janela) : IRagService
{
    private const int ConsultaMaxChars = 2000;

    public async Task<ConsultaPreparada> PrepararAsync(
        PecaInput input, Caso caso, IReadOnlyList<CasoDocumento> documentos, CancellationToken cancellationToken = default)
    {
        var consulta = await consultas.ExtrairAsync(input, documentos, cancellationToken);

        var semanticaFatos = Cortar($"{PecaCatalogo.RotuloArea(input.Area)}. {input.Pedidos}\n{input.Fatos}");
        var semanticaTese = Cortar($"{consulta.Tese}. {string.Join(", ", consulta.Temas)}");
        var textos = new[] { semanticaFatos, semanticaTese }.Where(t => t.Trim(' ', '.').Length > 0).Distinct().ToList();
        var vetores = await embeddings.EmbedAsync(textos, cancellationToken);
        if (vetores.Count != textos.Count)
        {
            throw new LlmServiceException("O serviço de embeddings devolveu um número inesperado de vetores.");
        }

        var exato = string.Join(' ', new[]
        {
            consulta.Tese, string.Join(' ', consulta.Temas), string.Join(' ', consulta.DispositivosSuspeitos), input.Pedidos,
        });
        var referencias = ExtratorReferencias.Extrair(
            $"{input.Fatos}\n{input.Pedidos}\n{input.InstrucoesAdicionais}\n{Combinados(consulta.DispositivosSuspeitos)}");

        return new ConsultaPreparada(consulta, vetores, Cortar(exato), referencias, caso.Uf, input.DataFatos);
    }

    public Task<IReadOnlyList<VectorHit>> BuscarCasoAsync(
        FiltroCaso filtro, ConsultaPreparada consulta, CancellationToken cancellationToken = default) =>
        buscaCaso.BuscarAsync(filtro, consulta, cancellationToken);

    public Task<IReadOnlyList<VectorHit>> BuscarJuridicoAsync(
        ConsultaPreparada consulta, CancellationToken cancellationToken = default) =>
        buscaJuridica.BuscarAsync(consulta, cancellationToken);

    public RagContext Montar(PecaInput input, IReadOnlyList<VectorHit> caso, IReadOnlyList<VectorHit> juridico)
    {
        var chars = rag.Value.CharsPorToken;
        var orcamento = Math.Min(rag.Value.MaxTokensContexto, OrcamentoContexto.Disponivel(input, janela.Value, chars));

        return MontadorContexto.Montar(caso, juridico, busca.Value, orcamento, chars);
    }

    /// <summary>O modelo às vezes separa a lei do artigo ("Código Civil", "art. 389"): junta para o extrator casar os dois.</summary>
    internal static string Combinados(IReadOnlyList<string> dispositivos)
    {
        var norma = dispositivos.FirstOrDefault(d => NormasConhecidas.PorNome(d) is not null);
        var linhas = dispositivos.Select(d =>
            norma is not null && d.StartsWith("art", StringComparison.OrdinalIgnoreCase) && ExtratorReferencias.Extrair(d).Count == 0
                ? $"{d} do {norma}"
                : d);

        return string.Join('\n', linhas);
    }

    private static string Cortar(string texto) => texto.Length <= ConsultaMaxChars ? texto : texto[..ConsultaMaxChars];
}
