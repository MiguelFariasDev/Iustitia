using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class RagServiceTests
{
    private static RagService Service(FakeVectorStore store, FakeEmbeddings embeddings, RagOptions? options = null) =>
        new(embeddings, store, Options.Create(options ?? new RagOptions()), NullLogger<RagService>.Instance);

    private static RagQuery Query(params Guid[] documentos) =>
        new(Ids.Tenant, "peticao_inicial", "civel", "Fatos do caso.", "Pedidos do caso.", documentos);

    [Fact]
    public async Task BuscaSempreFiltradaPeloTenantEPelaAreaDoPedido()
    {
        var store = new FakeVectorStore([Samples.Hit("a", "Trecho.", 0.9)]);

        await Service(store, new FakeEmbeddings()).RetrieveAsync(Query());

        store.LastQuery!.TenantId.Should().Be(Ids.Tenant);
        store.LastQuery.Area.Should().Be("civel");
    }

    [Fact]
    public async Task PedeOEmbeddingDoTipoAreaPedidosEFatos()
    {
        var embeddings = new FakeEmbeddings();

        await Service(new FakeVectorStore(), embeddings).RetrieveAsync(Query());

        var consulta = embeddings.Calls.Should().ContainSingle().Which.Should().ContainSingle().Which;
        consulta.Should().Contain("petição inicial").And.Contain("Pedidos do caso.").And.Contain("Fatos do caso.");
    }

    [Fact]
    public async Task DescartaHitDeOutraArea_MesmoSeOBancoDevolver()
    {
        var store = new FakeVectorStore(
        [
            Samples.Hit("a", "Cível.", 0.9, area: "civel"),
            Samples.Hit("b", "Trabalhista.", 0.95, area: "trabalhista"),
            Samples.Hit("c", "Sem área no payload.", 0.8, area: null),
        ]);

        var context = await Service(store, new FakeEmbeddings()).RetrieveAsync(Query());

        context.Chunks.Select(c => c.PointId).Should().Equal("a", "c");
    }

    [Fact]
    public async Task ComDocumentosEscolhidos_SoEntramTrechosDeles()
    {
        var escolhido = Guid.NewGuid();
        var store = new FakeVectorStore(
        [
            Samples.Hit("a", "Do documento escolhido.", 0.9, documento: escolhido),
            Samples.Hit("b", "De outro documento.", 0.99, documento: Guid.NewGuid()),
        ]);

        var context = await Service(store, new FakeEmbeddings()).RetrieveAsync(Query(escolhido));

        context.Chunks.Should().ContainSingle().Which.PointId.Should().Be("a");
        store.LastQuery!.DocumentosIds.Should().Equal(escolhido);
    }

    [Fact]
    public async Task RemoveDuplicatasPorHash_MantendoOdeMaiorScore()
    {
        var store = new FakeVectorStore(
        [
            Samples.Hit("baixo", "Mesmo texto.", 0.70, hash: "H1"),
            Samples.Hit("alto", "Mesmo texto.", 0.95, hash: "H1"),
            Samples.Hit("outro", "Texto diferente.", 0.80, hash: "H2"),
        ]);

        var context = await Service(store, new FakeEmbeddings()).RetrieveAsync(Query());

        context.Chunks.Select(c => c.PointId).Should().Equal("alto", "outro");
        context.Descartados.Should().Be(1);
    }

    [Fact]
    public async Task SemHashNoPayload_DeduplicaPeloTextoNormalizado()
    {
        var store = new FakeVectorStore(
        [
            Samples.Hit("a", "Texto   com  espaços.", 0.9),
            Samples.Hit("b", "texto com espaços.", 0.8),
        ]);

        var context = await Service(store, new FakeEmbeddings()).RetrieveAsync(Query());

        context.Chunks.Should().ContainSingle().Which.PointId.Should().Be("a");
    }

    [Fact]
    public async Task OrdenaPorScoreDecrescente()
    {
        var store = new FakeVectorStore(
        [
            Samples.Hit("meio", "B.", 0.5), Samples.Hit("topo", "A.", 0.9), Samples.Hit("fundo", "C.", 0.1),
        ]);

        var context = await Service(store, new FakeEmbeddings()).RetrieveAsync(Query());

        context.Chunks.Select(c => c.PointId).Should().Equal("topo", "meio", "fundo");
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(8, 8)]
    [InlineData(50, 10)]
    public async Task LimiteDeTrechosFicaEntre5E10(int configurado, int esperado)
    {
        var hits = Enumerable.Range(0, 30).Select(i => Samples.Hit($"p{i}", $"Trecho distinto {i}.", 1.0 - i * 0.01)).ToList();
        var store = new FakeVectorStore(hits);

        var context = await Service(store, new FakeEmbeddings(), new RagOptions { LimiteResultados = configurado })
            .RetrieveAsync(Query());

        context.Chunks.Should().HaveCount(esperado);
        store.LastQuery!.Limit.Should().Be(esperado * 2);
    }

    [Fact]
    public async Task LimitaOContextoAoOrcamentoDeTokens()
    {
        // 3.500 caracteres ≈ 1.000 tokens a 3,5 caracteres por token.
        var grande = new string('a', 3500);
        var hits = Enumerable.Range(0, 10).Select(i => Samples.Hit($"p{i}", grande + i, 1.0 - i * 0.01)).ToList();
        var store = new FakeVectorStore(hits);

        var context = await Service(
            store, new FakeEmbeddings(), new RagOptions { MaxContextTokens = 3000, CharsPorToken = 3.5, LimiteResultados = 10 })
            .RetrieveAsync(Query());

        context.Chunks.Should().HaveCount(2);
        context.TokensAproximados.Should().BeLessThanOrEqualTo(3000);
    }

    [Fact]
    public void ContextSelector_NaoPulaParaTrechoMenorDepoisQueUmMaiorNaoCouber_MasContinuaTentando()
    {
        var hits = new[]
        {
            Samples.Hit("a", new string('x', 700), 0.9),
            Samples.Hit("b", new string('y', 7000), 0.8),
            Samples.Hit("c", new string('z', 700), 0.7),
        };

        var context = ContextSelector.Select(hits, limit: 10, maxTokens: 500, charsPorToken: 3.5);

        context.Chunks.Select(c => c.PointId).Should().Equal("a", "c");
        context.Descartados.Should().Be(1);
    }

    [Fact]
    public void ContextSelector_AplicaScoreMinimo()
    {
        var hits = new[] { Samples.Hit("a", "Bom.", 0.9), Samples.Hit("b", "Ruim.", 0.2) };

        var context = ContextSelector.Select(hits, 10, 8000, 3.5, scoreMinimo: 0.5);

        context.Chunks.Should().ContainSingle().Which.PointId.Should().Be("a");
    }

    [Fact]
    public void ContextSelector_IgnoraTrechoVazio()
    {
        var context = ContextSelector.Select([Samples.Hit("a", "   ", 0.9)], 10, 8000, 3.5);

        context.Chunks.Should().BeEmpty();
    }
}
