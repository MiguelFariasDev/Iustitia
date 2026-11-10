using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Infrastructure.Qdrant;
using FluentAssertions;
using Qdrant.Client.Grpc;

namespace Advocacia.Modules.Drafting.Infrastructure.UnitTests;

public class QdrantServiceTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static string? Keyword(Condition condition) => condition.Field?.Match?.Keyword;

    [Fact]
    public void ToDaBuscaTemOFiltroDeTenantObrigatorio_MesmoSemNenhumOutroFiltro()
    {
        var filter = QdrantService.BuildFilter(new VectorSearchQuery(Tenant, [0.1f], 5));

        var condition = filter.Must.Should().ContainSingle().Subject;
        condition.Field.Key.Should().Be("tenantId");
        Keyword(condition).Should().Be(Tenant.ToString());
    }

    [Fact]
    public void OutrosFiltrosSaoAcrescentadosAoDeTenant_NuncaNoLugarDele()
    {
        var doc = Guid.NewGuid();
        var filter = QdrantService.BuildFilter(new VectorSearchQuery(
            Tenant, [0.1f], 5,
            Area: "civel",
            TipoDocumento: "contrato",
            PublicadoDe: new DateOnly(2024, 1, 1),
            PublicadoAte: new DateOnly(2024, 12, 31),
            DocumentosIds: [doc]));

        filter.Must.Select(c => c.Field.Key)
            .Should().BeEquivalentTo("tenantId", "area", "tipoDocumento", "documentoOrigemId", "dataPublicacao");
        filter.Must.First(c => c.Field.Key == "tenantId").Field.Match.Keyword.Should().Be(Tenant.ToString());
        filter.Must.First(c => c.Field.Key == "area").Field.Match.Keyword.Should().Be("civel");
        filter.Must.First(c => c.Field.Key == "documentoOrigemId").Field.Match.Keywords.Strings
            .Should().Equal(doc.ToString());
        filter.Must.First(c => c.Field.Key == "dataPublicacao").Field.DatetimeRange.Should().NotBeNull();
    }

    [Fact]
    public void FiltroDeExclusaoTambemEscopaPorTenant()
    {
        var filter = QdrantService.TenantFilter(Tenant);

        filter.Must.Should().ContainSingle().Which.Field.Key.Should().Be("tenantId");
    }

    [Fact]
    public void ConverteOPontoComTenantETodoOPayloadEDeVolta()
    {
        var doc = Guid.NewGuid();
        var meta = new ChunkMetadata(
            doc, "contrato.pdf", 7, "contrato", "civel", "TJSP", new DateOnly(2024, 5, 1), "https://exemplo.com/c", "HASH");
        var pointId = Guid.NewGuid().ToString();
        var point = QdrantService.ToPoint(new VectorPoint(Tenant, pointId, [0.1f, 0.2f], "Texto do trecho.", meta));

        point.Payload["tenantId"].StringValue.Should().Be(Tenant.ToString());
        point.Payload["pagina"].IntegerValue.Should().Be(7);
        point.Payload["dataPublicacao"].StringValue.Should().Be("2024-05-01T00:00:00Z");

        var scored = new ScoredPoint { Id = Guid.Parse(pointId), Score = 0.83f };
        foreach (var (key, value) in point.Payload)
        {
            scored.Payload[key] = value;
        }

        var hit = QdrantService.ToHit(scored);

        hit.PointId.Should().Be(pointId);
        hit.Texto.Should().Be("Texto do trecho.");
        hit.Score.Should().BeApproximately(0.83, 0.001);
        hit.Meta.Should().BeEquivalentTo(meta);
    }

    [Fact]
    public void PayloadSemCamposOpcionais_NaoQuebraALeitura()
    {
        var scored = new ScoredPoint { Id = Guid.NewGuid(), Score = 0.5f };
        scored.Payload["texto"] = new Value { StringValue = "Só o texto." };

        var hit = QdrantService.ToHit(scored);

        hit.Texto.Should().Be("Só o texto.");
        hit.Meta.NomeArquivo.Should().Be("documento");
        hit.Meta.Pagina.Should().BeNull();
        hit.Meta.DataPublicacao.Should().BeNull();
        hit.Meta.DocumentoOrigemId.Should().BeNull();
    }
}
