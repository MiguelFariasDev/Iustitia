using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Qdrant.Client.Grpc;

namespace Advocacia.Modules.Drafting.Infrastructure.Qdrant;

/// <summary>
/// As cinco coleções: vetor denso BGE-M3 (cosseno) em disco com quantização int8 na RAM, vetor esparso para o
/// BM25 com IDF calculado pelo servidor, payload em disco e índice em todo campo filtrado. Idempotente.
/// O Markdown integral nunca entra aqui: só o texto do trecho e a referência ao documento.
/// </summary>
public sealed class QdrantService(QdrantConexao conexao, ILogger<QdrantService> logger) : IQdrantService, IDisposable
{
    private static readonly string[] IndicesNorma =
    [
        "normaJuridicaId", "urn", "tipo", "numero", "situacao", "versaoTexto", "esfera", "uf", "municipio", "area", "tribunal",
        "assuntos", "artigo", "hashConteudo",
    ];

    private static readonly string[] IndicesCaso =
        ["clienteId", "documentoId", "usuarioIdResponsavel", "permissao", "origem", "tipoDocumento", "hash"];

    private readonly SemaphoreSlim _trava = new(1, 1);
    private bool _garantidas;

    public async Task GarantirColecoesAsync(CancellationToken cancellationToken = default)
    {
        if (_garantidas)
        {
            return;
        }

        await _trava.WaitAsync(cancellationToken);
        try
        {
            if (_garantidas)
            {
                return;
            }

            foreach (var colecao in Colecoes.Todas)
            {
                await conexao.ExecutarAsync(async cliente =>
                {
                    if (!await cliente.CollectionExistsAsync(colecao, cancellationToken))
                    {
                        await CriarAsync(colecao, cancellationToken);
                    }

                    return true;
                });
            }

            _garantidas = true;
        }
        finally
        {
            _trava.Release();
        }
    }

    public async Task<bool> VerificarSaudeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await conexao.Cliente.HealthAsync(cancellationToken);

            return true;
        }
        catch (Exception ex) when (ex is RpcException or HttpRequestException or TimeoutException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<ColecaoInfo>> EstadoAsync(CancellationToken cancellationToken = default)
    {
        var lista = new List<ColecaoInfo>();
        foreach (var colecao in Colecoes.Todas)
        {
            lista.Add(await conexao.ExecutarAsync(async cliente =>
                await cliente.CollectionExistsAsync(colecao, cancellationToken)
                    ? new ColecaoInfo(colecao, true, await cliente.CountAsync(colecao, exact: false, cancellationToken: cancellationToken))
                    : new ColecaoInfo(colecao, false, 0)));
        }

        return lista;
    }

    public void Dispose() => _trava.Dispose();

    private async Task CriarAsync(string colecao, CancellationToken ct)
    {
        var cliente = conexao.Cliente;
        var densos = new VectorParamsMap();
        densos.Map[QdrantConexao.Denso] = new VectorParams
        {
            Size = EmbeddingConstants.Dimensoes,
            Distance = Distance.Cosine,
            // A Constituição cabe na RAM com folga; o resto cresce e fica em disco.
            OnDisk = conexao.Opcoes.VetoresEmDisco && colecao != Colecoes.ConstituicaoFederal,
        };
        var esparsos = new SparseVectorConfig();
        esparsos.Map[QdrantConexao.Esparso] = new SparseVectorParams
        {
            Modifier = Modifier.Idf,
            Index = new SparseIndexConfig { OnDisk = conexao.Opcoes.VetoresEmDisco },
        };

        await cliente.CreateCollectionAsync(
            colecao,
            densos,
            onDiskPayload: true,
            quantizationConfig: new QuantizationConfig
            {
                Scalar = new ScalarQuantization { Type = QuantizationType.Int8, Quantile = 0.99f, AlwaysRam = true },
            },
            sparseVectorsConfig: esparsos,
            cancellationToken: ct);

        if (colecao is Colecoes.DocumentosCaso or Colecoes.PecasModelo)
        {
            // "Tenant" do Qdrant: pontos do mesmo escritório e do mesmo caso ficam juntos no disco, e o filtro é barato.
            foreach (var campo in new[] { CasoQdrantStore.TenantKey, CasoQdrantStore.CasoKey })
            {
                await cliente.CreatePayloadIndexAsync(
                    colecao, campo, PayloadSchemaType.Keyword,
                    new PayloadIndexParams { KeywordIndexParams = new KeywordIndexParams { IsTenant = true } },
                    cancellationToken: ct);
            }

            foreach (var campo in IndicesCaso)
            {
                await cliente.CreatePayloadIndexAsync(colecao, campo, PayloadSchemaType.Keyword, cancellationToken: ct);
            }

            await cliente.CreatePayloadIndexAsync(colecao, "suspeito", PayloadSchemaType.Bool, cancellationToken: ct);
            await cliente.CreatePayloadIndexAsync(colecao, "dataUpload", PayloadSchemaType.Datetime, cancellationToken: ct);
        }
        else
        {
            foreach (var campo in IndicesNorma)
            {
                await cliente.CreatePayloadIndexAsync(colecao, campo, PayloadSchemaType.Keyword, cancellationToken: ct);
            }

            await cliente.CreatePayloadIndexAsync(colecao, "revogada", PayloadSchemaType.Bool, cancellationToken: ct);
            await cliente.CreatePayloadIndexAsync(colecao, "versao", PayloadSchemaType.Integer, cancellationToken: ct);
            await cliente.CreatePayloadIndexAsync(colecao, "ano", PayloadSchemaType.Integer, cancellationToken: ct);
            await cliente.CreatePayloadIndexAsync(colecao, "data", PayloadSchemaType.Datetime, cancellationToken: ct);
        }

        logger.LogInformation("Qdrant: coleção '{Colecao}' criada.", colecao);
    }
}
