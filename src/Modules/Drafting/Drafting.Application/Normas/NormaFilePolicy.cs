namespace Advocacia.Modules.Drafting.Application.Normas;

/// <summary>
/// Quais arquivos do repositório entram na ingestão — regra PURA (só strings), usada tanto ao listar o
/// disco quanto ao decidir o que "sumiu". Segue a política: só Markdown, só pastas incluídas, nunca
/// _fonte/, .git/, scripts/ nem qualquer segmento iniciado por "_" (auditorias, lotes, índices) ou ".".
/// </summary>
public static class NormaFilePolicy
{
    public static bool Elegivel(string caminhoRelativo, NormasIngestaoOptions opcoes)
    {
        var caminho = caminhoRelativo.Replace('\\', '/').TrimStart('/');
        if (!caminho.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segmentos = caminho.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segmento in segmentos)
        {
            if (segmento.StartsWith('_') || segmento.StartsWith('.'))
            {
                return false;
            }

            if (opcoes.PastasExcluidas.Any(excluida => string.Equals(excluida.Trim('/'), segmento, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return opcoes.PastasIncluidas.Any(pasta => DentroDe(caminho, pasta));
    }

    /// <summary>Ordem de ingestão: o que mais pesa numa minuta primeiro, o volume enorme por último.</summary>
    public static int Prioridade(string caminhoRelativo)
    {
        var c = caminhoRelativo.Replace('\\', '/').ToLowerInvariant();

        return c switch
        {
            "constituicao.md" => 0,
            _ when c.StartsWith("federal/leis-complementares/", StringComparison.Ordinal)
                || c.StartsWith("federal/leis-ordinarias/", StringComparison.Ordinal)
                || c.StartsWith("federal/decretos-lei/", StringComparison.Ordinal) => 1,
            _ when c.Contains("/sumulas/", StringComparison.Ordinal) => 2,
            _ when c.StartsWith("jurisprudencia/", StringComparison.Ordinal) => 3,
            _ when c.StartsWith("tratados/", StringComparison.Ordinal) => 4,
            _ when c.StartsWith("federal/", StringComparison.Ordinal) => 5,
            _ when c.StartsWith("estaduais/", StringComparison.Ordinal) || c.StartsWith("municipais/", StringComparison.Ordinal) => 6,
            _ => 7,
        };
    }

    public static bool DentroDe(string caminho, string pasta)
    {
        var p = pasta.Replace('\\', '/').Trim('/');

        return caminho.Equals(p, StringComparison.OrdinalIgnoreCase)
            || caminho.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Recorte pedido pelo operador (UF, município, tipo, área, período) aplicado sobre o metadado resolvido.</summary>
    public static string? MotivoDeRecusa(Advocacia.Modules.Drafting.Domain.Normas.NormaMetadata meta, NormasFiltroOptions filtro)
    {
        if (filtro.Ufs.Length > 0 && (meta.Esfera is "estadual" or "municipal")
            && !filtro.Ufs.Contains(meta.Uf ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            return "UF fora do recorte";
        }

        if (filtro.Municipios.Length > 0 && meta.Esfera == "municipal"
            && !filtro.Municipios.Contains(meta.Municipio ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            return "município fora do recorte";
        }

        if (filtro.Tipos.Length > 0 && !filtro.Tipos.Any(t => meta.Tipo.Contains(t, StringComparison.OrdinalIgnoreCase)))
        {
            return "tipo fora do recorte";
        }

        if (filtro.Areas.Length > 0 && !filtro.Areas.Contains(meta.Area ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            return "área fora do recorte";
        }

        if (meta.Ano is { } ano && ((filtro.AnoDe is { } de && ano < de) || (filtro.AnoAte is { } ate && ano > ate)))
        {
            return "período fora do recorte";
        }

        return null;
    }
}
