using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Domain.Casos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Busca;

/// <summary>
/// Busca híbrida nos anexos de UM caso. O isolamento tem três travas independentes:
/// (1) <see cref="FiltroCaso"/> não existe sem tenant e caso; (2) o banco vetorial aplica o filtro em toda
/// consulta; (3) cada resultado é conferido aqui contra o filtro E contra o cadastro de documentos do caso —
/// trecho de documento removido, de outro caso ou restrito a outra pessoa é descartado e vira alerta.
/// </summary>
public sealed class BuscaDocumentosCasoService(
    ICasoVectorStore vetores,
    ICasoDocumentoRepository documentos,
    IRerankerService reranker,
    ISegurancaIaService seguranca,
    IOptions<BuscaNormasOptions> options,
    ILogger<BuscaDocumentosCasoService> logger) : IBuscaDocumentosCasoService
{
    public async Task<IReadOnlyList<VectorHit>> BuscarAsync(
        FiltroCaso filtro, ConsultaPreparada consulta, CancellationToken cancellationToken = default)
    {
        var cadastro = (await documentos.ListarAsync(CasoId.From(filtro.CasoId), cancellationToken))
            .ToDictionary(d => d.Id.Value);
        if (cadastro.Count == 0)
        {
            return [];
        }

        var opcoes = options.Value;
        var listas = new List<ListaRankeada>();
        foreach (var vetor in consulta.Vetores)
        {
            listas.Add(new ListaRankeada(
                "vetor",
                await vetores.BuscarDensoAsync(filtro, vetor, opcoes.CandidatosVetor, cancellationToken),
                1.0 / Math.Max(1, consulta.Vetores.Count)));
        }

        var esparso = Bm25.Consulta(consulta.TextoBm25);
        if (!esparso.Vazio)
        {
            listas.Add(new ListaRankeada("bm25", await Bm25SeguroAsync(filtro, esparso, opcoes.CandidatosBm25, cancellationToken)));
        }

        var fundidos = FusaoRanking.Fundir(opcoes.Fusao, listas, opcoes.RrfK);
        var permitidos = new List<CandidatoBusca>();
        var recusados = 0;
        foreach (var candidato in fundidos)
        {
            if (Conferir(candidato.Hit, filtro, cadastro) is { } hit)
            {
                permitidos.Add(candidato with { Hit = hit });
            }
            else
            {
                recusados++;
            }
        }

        if (recusados > 0)
        {
            // Não deveria acontecer nunca: ou o índice está com lixo, ou alguém está sondando.
            logger.LogError("Busca do caso {CasoId}: {N} trecho(s) recusado(s) pela conferência de isolamento.", filtro.CasoId, recusados);
            await seguranca.RegistrarAsync(
                filtro.TenantId, filtro.UsuarioId, "TrechoForaDoCaso", filtro.CasoId.ToString(), null, cancellationToken);
        }

        var ordenados = reranker.Reordenar(permitidos, BuscaJuridicaService.Contexto(consulta));
        var final = BuscaJuridicaService.Deduplicar(ordenados, int.MaxValue, opcoes.ResultadosMax * 2);
        logger.LogInformation(
            "Busca do caso: {Fundidos} candidatos, {Final} após reranking.", fundidos.Count, final.Count);

        return final;
    }

    /// <summary>Nulo = trecho proibido. O estado de revisão vem do cadastro (o payload do índice fica velho).</summary>
    internal static VectorHit? Conferir(VectorHit hit, FiltroCaso filtro, IReadOnlyDictionary<Guid, CasoDocumento> cadastro)
    {
        if (hit.Meta.Caso is not { } caso
            || caso.TenantId != filtro.TenantId
            || caso.CasoId != filtro.CasoId
            || !cadastro.TryGetValue(caso.DocumentoId, out var documento)
            || documento.TenantId != filtro.TenantId
            || documento.CasoId.Value != filtro.CasoId)
        {
            return null;
        }

        if (documento.Restrito && !filtro.VeRestritos && documento.UsuarioIdResponsavel != filtro.UsuarioId)
        {
            return null;
        }

        if (filtro.DocumentosIds.Count > 0 && !filtro.DocumentosIds.Contains(caso.DocumentoId))
        {
            return null;
        }

        return hit with
        {
            Meta = hit.Meta with
            {
                TipoDocumento = documento.TipoDocumento ?? hit.Meta.TipoDocumento,
                Caso = caso with { RevisaoPendente = documento.PendenteDeRevisao, Restrito = documento.Restrito },
            },
        };
    }

    private async Task<IReadOnlyList<VectorHit>> Bm25SeguroAsync(
        FiltroCaso filtro, VetorEsparso esparso, int limite, CancellationToken ct)
    {
        try
        {
            return await vetores.BuscarBm25Async(filtro, esparso, limite, ct);
        }
        catch (VectorStoreException ex)
        {
            logger.LogWarning(ex, "BM25 indisponível nos anexos; seguindo só com a busca vetorial.");

            return [];
        }
    }
}
