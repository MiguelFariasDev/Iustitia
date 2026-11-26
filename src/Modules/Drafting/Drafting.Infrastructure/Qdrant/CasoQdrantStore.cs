using System.Globalization;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Domain.Casos;
using Qdrant.Client.Grpc;

namespace Advocacia.Modules.Drafting.Infrastructure.Qdrant;

/// <summary>
/// Coleção <c>documentos_caso</c>. O Qdrant não tem RLS: TODA leitura e TODA exclusão passam por
/// <see cref="Base"/> (tenant + caso). Não há método que aceite filtro livre.
/// </summary>
public sealed class CasoQdrantStore(QdrantConexao conexao) : ICasoVectorStore
{
    public const string TenantKey = "tenantId";
    public const string CasoKey = "casoId";
    public const string PermissaoCaso = "caso";
    public const string PermissaoRestrito = "restrito";

    private const int Lote = 64;

    public async Task UpsertAsync(IReadOnlyList<CasoVectorPoint> pontos, CancellationToken cancellationToken = default)
    {
        for (var inicio = 0; inicio < pontos.Count; inicio += Lote)
        {
            var lote = pontos.Skip(inicio).Take(Lote).Select(ToPoint).ToList();
            await conexao.ExecutarAsync(c => c.UpsertAsync(Colecoes.DocumentosCaso, lote, wait: true, cancellationToken: cancellationToken));
        }
    }

    public Task RemoverDocumentoAsync(Guid tenantId, Guid casoId, Guid documentoId, CancellationToken cancellationToken = default) =>
        conexao.ExecutarAsync(async c =>
        {
            if (!await c.CollectionExistsAsync(Colecoes.DocumentosCaso, cancellationToken))
            {
                return true;
            }

            var filtro = Base(tenantId, casoId);
            filtro.Must.Add(Conditions.MatchKeyword("documentoId", documentoId.ToString()));
            await c.DeleteAsync(Colecoes.DocumentosCaso, filtro, wait: true, cancellationToken: cancellationToken);

            return true;
        });

    public async Task<IReadOnlyList<VectorHit>> BuscarDensoAsync(
        FiltroCaso filtro, float[] vetor, int limite, CancellationToken cancellationToken = default) =>
        (await conexao.BuscarAsync(Colecoes.DocumentosCaso, vetor, QdrantConexao.Denso, Filtro(filtro), limite, cancellationToken))
        .Select(ToHit).ToList();

    public async Task<IReadOnlyList<VectorHit>> BuscarBm25Async(
        FiltroCaso filtro, VetorEsparso consulta, int limite, CancellationToken cancellationToken = default) =>
        (await conexao.BuscarAsync(
            Colecoes.DocumentosCaso, (consulta.Valores, consulta.Indices), QdrantConexao.Esparso, Filtro(filtro), limite, cancellationToken))
        .Select(ToHit).ToList();

    /// <summary>O mínimo de qualquer operação. Outros filtros são ACRESCENTADOS a ele, nunca no lugar dele.</summary>
    internal static Filter Base(Guid tenantId, Guid casoId)
    {
        if (tenantId == Guid.Empty || casoId == Guid.Empty)
        {
            throw new ArgumentException("Operação em documentos do caso exige tenant e caso.");
        }

        var filtro = new Filter();
        filtro.Must.Add(Conditions.MatchKeyword(TenantKey, tenantId.ToString()));
        filtro.Must.Add(Conditions.MatchKeyword(CasoKey, casoId.ToString()));

        return filtro;
    }

    internal static Filter Filtro(FiltroCaso f)
    {
        var filtro = Base(f.TenantId, f.CasoId);

        if (!f.VeRestritos)
        {
            // Aberto ao caso, OU restrito mas anexado por quem pede. Filtro ANINHADO (OU) dentro do MUST.
            var permissao = new Filter();
            permissao.Should.Add(Conditions.MatchKeyword("permissao", PermissaoCaso));
            permissao.Should.Add(Conditions.MatchKeyword("usuarioIdResponsavel", f.UsuarioId.ToString()));
            filtro.Must.Add(Conditions.Filter(permissao));
        }

        if (f.DocumentosIds.Count > 0)
        {
            filtro.Must.Add(Conditions.Match("documentoId", f.DocumentosIds.Select(id => id.ToString()).ToList()));
        }

        // Trecho que tenta dar ordens ao assistente nem chega a ser candidato.
        filtro.MustNot.Add(Conditions.Match("suspeito", true));

        return filtro;
    }

    internal static PointStruct ToPoint(CasoVectorPoint p)
    {
        var meta = p.Meta;
        var caso = meta.Caso ?? throw new ArgumentException("Ponto de documento do caso sem identificação do caso.");
        var ponto = new PointStruct { Id = p.Id, Vectors = QdrantConexao.Vetores(p.Denso, p.Esparso) };

        ponto.Payload[TenantKey] = QdrantConexao.Texto(caso.TenantId.ToString());
        ponto.Payload[CasoKey] = QdrantConexao.Texto(caso.CasoId.ToString());
        ponto.Payload["clienteId"] = QdrantConexao.Texto(caso.ClienteId.ToString());
        ponto.Payload["documentoId"] = QdrantConexao.Texto(caso.DocumentoId.ToString());
        ponto.Payload["usuarioIdResponsavel"] = QdrantConexao.Texto(caso.UsuarioIdResponsavel.ToString());
        ponto.Payload["permissao"] = QdrantConexao.Texto(caso.Restrito ? PermissaoRestrito : PermissaoCaso);
        ponto.Payload["origem"] = QdrantConexao.Texto(caso.Origem.ToString().ToLowerInvariant());
        ponto.Payload["hash"] = QdrantConexao.Texto(meta.HashConteudo);
        ponto.Payload["nomeArquivo"] = QdrantConexao.Texto(meta.NomeArquivo);
        ponto.Payload["texto"] = QdrantConexao.Texto(p.Texto);
        ponto.Payload["suspeito"] = new Value { BoolValue = caso.Suspeito };
        ponto.Payload["dataUpload"] = QdrantConexao.Texto(
            p.DataUpload.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        QdrantConexao.Definir(ponto, "tipoDocumento", meta.TipoDocumento);
        QdrantConexao.Definir(ponto, "area", meta.Area);
        QdrantConexao.Definir(ponto, "secao", caso.Secao);
        QdrantConexao.Definir(ponto, "dataDocumento", meta.DataPublicacao?.Rfc3339());

        if (meta.Pagina is { } pagina)
        {
            ponto.Payload["pagina"] = new Value { IntegerValue = pagina };
        }

        if (caso.Confianca is { } confianca)
        {
            ponto.Payload["confianca"] = new Value { DoubleValue = confianca };
        }

        return ponto;
    }

    internal static VectorHit ToHit(ScoredPoint ponto)
    {
        var p = ponto.Payload;
        var documentoId = p.Id("documentoId") ?? Guid.Empty;

        // Payload sem tenant ou caso vira Guid.Empty, que a conferência da busca recusa.
        var caso = new CasoRef(
            p.Id(TenantKey) ?? Guid.Empty,
            p.Id(CasoKey) ?? Guid.Empty,
            p.Id("clienteId") ?? Guid.Empty,
            documentoId,
            p.Id("usuarioIdResponsavel") ?? Guid.Empty,
            Enum.TryParse<OrigemTexto>(p.Str("origem"), ignoreCase: true, out var origem) ? origem : OrigemTexto.Ocr,
            p.Num("confianca"),
            p.Str("secao"),
            p.Str("permissao") != PermissaoCaso,
            p.Bool("suspeito"));

        return new VectorHit(
            ponto.Id.Uuid,
            p.Str("texto") ?? string.Empty,
            ponto.Score,
            new ChunkMetadata(
                documentoId, p.Str("nomeArquivo") ?? "documento", (int?)p.Int("pagina"), p.Str("tipoDocumento"), p.Str("area"), null,
                p.Data("dataDocumento"), null, p.Str("hash") ?? string.Empty, Caso: caso));
    }
}
