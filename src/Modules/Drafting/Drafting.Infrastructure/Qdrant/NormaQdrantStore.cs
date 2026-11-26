using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Qdrant.Client.Grpc;

namespace Advocacia.Modules.Drafting.Infrastructure.Qdrant;

/// <summary>
/// Coleções do acervo normativo PÚBLICO (constituicao_federal, leis_federais, jurisprudencia). Sem filtro de
/// tenant — o acervo é compartilhado e nunca contém dado de cliente; anexos ficam em
/// <see cref="CasoQdrantStore"/>, que SEMPRE filtra por tenant e caso.
/// </summary>
public sealed class NormaQdrantStore(QdrantConexao conexao) : INormaVectorStore
{
    private const int Lote = 64;

    private static readonly string[] EsferasSemUf = ["federal", "jurisprudencia"];

    public async Task UpsertAsync(string colecao, IReadOnlyList<NormaVectorPoint> points, CancellationToken cancellationToken = default)
    {
        Publica(colecao);
        for (var inicio = 0; inicio < points.Count; inicio += Lote)
        {
            var lote = points.Skip(inicio).Take(Lote).Select(ToPoint).ToList();
            await conexao.ExecutarAsync(c => c.UpsertAsync(colecao, lote, wait: true, cancellationToken: cancellationToken));
        }
    }

    public Task DeleteAsync(string colecao, Guid normaId, int? manterVersao, CancellationToken cancellationToken = default) =>
        conexao.ExecutarAsync(async c =>
        {
            Publica(colecao);
            if (!await c.CollectionExistsAsync(colecao, cancellationToken))
            {
                return true;
            }

            var filtro = new Filter();
            filtro.Must.Add(Conditions.MatchKeyword("normaJuridicaId", normaId.ToString()));
            if (manterVersao is { } versao)
            {
                filtro.MustNot.Add(Conditions.Match("versao", (long)versao));
            }

            await c.DeleteAsync(colecao, filtro, wait: true, cancellationToken: cancellationToken);

            return true;
        });

    public async Task<IReadOnlyList<VectorHit>> BuscarDensoAsync(
        string colecao, NormaFiltro filtro, float[] vetor, int limite, CancellationToken cancellationToken = default)
    {
        Publica(colecao);

        return (await conexao.BuscarAsync(colecao, vetor, QdrantConexao.Denso, BuildFilter(filtro), limite, cancellationToken))
            .Select(ToHit).ToList();
    }

    public async Task<IReadOnlyList<VectorHit>> BuscarBm25Async(
        string colecao, NormaFiltro filtro, VetorEsparso consulta, int limite, CancellationToken cancellationToken = default)
    {
        Publica(colecao);

        return (await conexao.BuscarAsync(
                colecao, (consulta.Valores, consulta.Indices), QdrantConexao.Esparso, BuildFilter(filtro), limite, cancellationToken))
            .Select(ToHit).ToList();
    }

    /// <summary>Esta classe não tem filtro de tenant; por isso não pode nem por engano tocar na coleção de anexos.</summary>
    private static void Publica(string colecao)
    {
        if (!Colecoes.Publicas.Contains(colecao, StringComparer.Ordinal))
        {
            throw new ArgumentException($"'{colecao}' não é uma coleção pública.", nameof(colecao));
        }
    }

    internal static Filter BuildFilter(NormaFiltro f)
    {
        var filter = new Filter();

        if (!f.IncluirRevogadas)
        {
            filter.Must.Add(Conditions.Match("revogada", false));
        }

        AddAny(filter, "tipo", f.Tipos);
        AddAny(filter, "tribunal", f.Tribunais?.Select(t => t.ToUpperInvariant()).ToList());
        AddAny(filter, "esfera", f.Esferas);
        AddAny(filter, "assuntos", f.Assuntos);

        if (f.NormaIds is { Count: > 0 } ids)
        {
            filter.Must.Add(Conditions.Match("normaJuridicaId", ids.Select(i => i.ToString()).ToList()));
        }

        if (f.Ufs is { Count: > 0 } ufs)
        {
            // Restringe só o que TEM UF; federal e tribunais nacionais continuam valendo.
            var uf = new Filter();
            uf.Should.Add(Conditions.Match("uf", ufs.Select(u => u.ToUpperInvariant()).ToList()));
            uf.Should.Add(Conditions.Match("esfera", EsferasSemUf.ToList()));
            filter.Must.Add(Conditions.Filter(uf));
        }

        AddAny(filter, "municipio", f.Municipios);

        if (f.Areas is { Count: > 0 } areas)
        {
            // Sem área no payload = área desconhecida, não "outra área": passa.
            var area = new Filter();
            area.Should.Add(Conditions.Match("area", areas.ToList()));
            area.Should.Add(Conditions.IsEmpty("area"));
            filter.Must.Add(Conditions.Filter(area));
        }

        if (f.DataDe is not null || f.DataAte is not null)
        {
            filter.Must.Add(Conditions.DatetimeRange(
                "data",
                gte: f.DataDe?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                lte: f.DataAte?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)));
        }

        return filter;
    }

    private static void AddAny(Filter filter, string key, IReadOnlyList<string>? values)
    {
        if (values is { Count: > 0 })
        {
            filter.Must.Add(Conditions.Match(key, values.ToList()));
        }
    }

    internal static PointStruct ToPoint(NormaVectorPoint point)
    {
        var p = point.Payload;
        var result = new PointStruct { Id = point.Id, Vectors = QdrantConexao.Vetores(point.Vector, point.Esparso) };

        result.Payload["fonte"] = QdrantConexao.Texto("ctb_leis");
        result.Payload["normaJuridicaId"] = QdrantConexao.Texto(p.NormaJuridicaId.ToString());
        result.Payload["titulo"] = QdrantConexao.Texto(p.Titulo);
        result.Payload["tipo"] = QdrantConexao.Texto(p.Tipo);
        result.Payload["situacao"] = QdrantConexao.Texto(p.Situacao);
        result.Payload["versaoTexto"] = QdrantConexao.Texto(p.VersaoTexto);
        result.Payload["esfera"] = QdrantConexao.Texto(p.Esfera);
        result.Payload["colecao"] = QdrantConexao.Texto(p.Colecao);
        result.Payload["arquivo"] = QdrantConexao.Texto(p.ArquivoMarkdownPath);
        result.Payload["fonteOficialUrl"] = QdrantConexao.Texto(p.FonteOficialUrl);
        result.Payload["hashConteudo"] = QdrantConexao.Texto(p.HashConteudo);
        result.Payload["texto"] = QdrantConexao.Texto(p.Texto);
        result.Payload["revogada"] = new Value { BoolValue = p.Revogada };
        result.Payload["versao"] = new Value { IntegerValue = p.VersaoNumero };
        QdrantConexao.Definir(result, "urn", p.Urn);
        QdrantConexao.Definir(result, "numero", p.Numero);
        QdrantConexao.Definir(result, "uf", p.Uf);
        QdrantConexao.Definir(result, "municipio", p.Municipio);
        QdrantConexao.Definir(result, "area", p.Area);
        QdrantConexao.Definir(result, "tribunal", p.Tribunal?.ToUpperInvariant());
        QdrantConexao.Definir(result, "secao", p.Secao);
        QdrantConexao.Definir(result, "artigo", p.Artigo);
        QdrantConexao.Definir(result, "paragrafo", p.Paragrafo);
        QdrantConexao.Definir(result, "inciso", p.Inciso);
        QdrantConexao.Definir(result, "data", p.Data?.Rfc3339());

        if (p.Pagina is { } pagina)
        {
            result.Payload["pagina"] = new Value { IntegerValue = pagina };
        }

        if (p.Data is { } data)
        {
            result.Payload["ano"] = new Value { IntegerValue = data.Year };
        }

        if (p.Assuntos.Count > 0)
        {
            var list = new ListValue();
            list.Values.AddRange(p.Assuntos.Select(QdrantConexao.Texto));
            result.Payload["assuntos"] = new Value { ListValue = list };
        }

        return result;
    }

    internal static VectorHit ToHit(ScoredPoint point)
    {
        var p = point.Payload;
        IReadOnlyList<string> assuntos = p.TryGetValue("assuntos", out var a) && a.KindCase == Value.KindOneofCase.ListValue
            ? a.ListValue.Values.Where(v => v.KindCase == Value.KindOneofCase.StringValue).Select(v => v.StringValue).ToList()
            : [];

        var payload = new NormaPayload(
            p.Id("normaJuridicaId") ?? Guid.Empty,
            p.Str("urn"),
            p.Str("titulo") ?? "norma",
            p.Str("tipo") ?? "Norma",
            p.Str("numero"),
            p.Data("data"),
            p.Str("situacao") ?? "Não informada",
            p.Bool("revogada"),
            p.Str("versaoTexto") ?? "Não informada",
            (int)(p.Int("versao") ?? 1),
            p.Str("esfera") ?? "federal",
            p.Str("uf"),
            p.Str("municipio"),
            p.Str("area"),
            p.Str("tribunal"),
            assuntos,
            p.Str("colecao") ?? string.Empty,
            p.Str("arquivo") ?? string.Empty,
            p.Str("fonteOficialUrl") ?? string.Empty,
            (int?)p.Int("pagina"),
            p.Str("secao"),
            p.Str("artigo"),
            p.Str("paragrafo"),
            p.Str("inciso"),
            p.Str("hashConteudo") ?? string.Empty,
            p.Str("texto") ?? string.Empty);

        return payload.ToHit(point.Id.Uuid, point.Score);
    }
}
