using System.Security.Cryptography;
using System.Text;
using Advocacia.Modules.Drafting.Application.Models;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Regras puras de seleção do contexto recuperado, isoladas para serem testadas sem rede:
/// remove duplicatas por hash do texto, ordena por score e corta no limite de trechos e de tokens.
/// </summary>
public static class ContextSelector
{
    public static RagContext Select(
        IEnumerable<VectorHit> hits, int limit, int maxTokens, double charsPorToken, double? scoreMinimo = null)
    {
        var chosen = new List<RetrievedChunk>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var tokens = 0;
        var discarded = 0;

        foreach (var hit in hits.OrderByDescending(h => h.Score))
        {
            if (chosen.Count >= limit)
            {
                break;
            }

            if (scoreMinimo is { } minimo && hit.Score < minimo)
            {
                discarded++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(hit.Texto) || !seen.Add(ContentHash(hit)))
            {
                discarded++;
                continue;
            }

            var chunkTokens = TokenEstimator.Estimate(hit.Texto, charsPorToken);
            if (tokens + chunkTokens > maxTokens)
            {
                // Não pula para um trecho menor e pior só para "encher": a lista está por relevância.
                discarded++;
                continue;
            }

            tokens += chunkTokens;
            chosen.Add(new RetrievedChunk(hit.PointId, hit.Texto, hit.Score, hit.Meta));
        }

        return new RagContext(chosen, tokens, discarded);
    }

    /// <summary>Hash do payload quando existe; senão do texto normalizado (espaços e caixa não contam).</summary>
    public static string ContentHash(VectorHit hit) =>
        !string.IsNullOrWhiteSpace(hit.Meta.HashConteudo) ? hit.Meta.HashConteudo : HashText(hit.Texto);

    public static string HashText(string text)
    {
        var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
