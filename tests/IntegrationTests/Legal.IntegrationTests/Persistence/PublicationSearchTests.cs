using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Legal.IntegrationTests.Common;
using FluentAssertions;

namespace Advocacia.Modules.Legal.IntegrationTests.Persistence;

/// <summary>
/// Filtros da tela de publicações contra um Postgres real: cada um precisa TRADUZIR para SQL
/// (ver QueryTranslationTests) e devolver só o que o escritório pode ver.
/// </summary>
[Collection(LegalPostgresCollection.Name)]
public class PublicationSearchTests(LegalPostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = TestPublications.Now;

    [Fact]
    public async Task FiltraPorTribunalClienteEPeriodo()
    {
        var tenantId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        var tjsp = Seed(tenantId, "s-1", TestPublications.CnjNumberA, Now.AddDays(-2), clienteId);
        var tjrj = Seed(tenantId, "s-2", TestPublications.CnjNumberB, Now.AddDays(-10), null);
        repository.AddRange([tjsp, tjrj]);
        await context.SaveChangesAsync();

        (await repository.SearchAsync(tenantId, new(Tribunal: "tjsp"), 0, 10)).Should().ContainSingle().Which.Id.Should().Be(tjsp.Id);
        (await repository.SearchAsync(tenantId, new(ClienteId: clienteId), 0, 10)).Should().ContainSingle().Which.Id.Should().Be(tjsp.Id);
        (await repository.SearchAsync(tenantId, new(From: DateOnly.FromDateTime(Now.AddDays(-5).UtcDateTime)), 0, 10))
            .Should().ContainSingle().Which.Id.Should().Be(tjsp.Id);
        (await repository.SearchAsync(tenantId, new(To: DateOnly.FromDateTime(Now.AddDays(-5).UtcDateTime)), 0, 10))
            .Should().ContainSingle().Which.Id.Should().Be(tjrj.Id);
    }

    [Fact]
    public async Task FiltraPorVariosStatusELeitura_OrdenandoDoMaisRecente()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        var antiga = Seed(tenantId, "o-1", TestPublications.CnjNumberA, Now.AddDays(-9), null);
        var recente = Seed(tenantId, "o-2", TestPublications.CnjNumberA, Now.AddDays(-1), null);
        var pendente = Seed(tenantId, "o-3", TestPublications.CnjNumberA, Now.AddDays(-3), null);
        antiga.ChangeStatus(PublicationStatus.Approved);
        recente.ChangeStatus(PublicationStatus.Corrected);
        recente.MarkAsRead();
        repository.AddRange([antiga, recente, pendente]);
        await context.SaveChangesAsync();

        var tratadas = await repository.SearchAsync(
            tenantId, new([PublicationStatus.Approved, PublicationStatus.Corrected]), 0, 10);

        tratadas.Select(p => p.Id).Should().Equal(recente.Id, antiga.Id);

        (await repository.CountSearchAsync(tenantId, new(IsRead: false))).Should().Be(2);
    }

    [Fact]
    public async Task OrdenaPorDataOuPorTribunal()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        var tjrjRecente = Seed(tenantId, "r-1", TestPublications.CnjNumberB, Now.AddDays(-1), null);
        var tjspAntiga = Seed(tenantId, "r-2", TestPublications.CnjNumberA, Now.AddDays(-5), null);
        repository.AddRange([tjrjRecente, tjspAntiga]);
        await context.SaveChangesAsync();

        async Task<Guid[]> Ids(PublicationSort sort) =>
            (await repository.SearchAsync(tenantId, new(Sort: sort), 0, 10)).Select(p => p.Id.Value).ToArray();

        (await Ids(PublicationSort.Recent)).Should().Equal(tjrjRecente.Id.Value, tjspAntiga.Id.Value);
        (await Ids(PublicationSort.Oldest)).Should().Equal(tjspAntiga.Id.Value, tjrjRecente.Id.Value);
        (await Ids(PublicationSort.TribunalAsc)).Should().Equal(tjrjRecente.Id.Value, tjspAntiga.Id.Value);
        (await Ids(PublicationSort.TribunalDesc)).Should().Equal(tjspAntiga.Id.Value, tjrjRecente.Id.Value);
    }

    [Fact]
    public async Task BuscaPorNumeroParcialDoProcessoEPorNomeDoCliente()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        var cliente = Cliente.Create(
            tenantId, TipoCliente.PessoaFisica, NomeCliente.Create("Maria Aparecida Silva").Value,
            Documento.Create("11144477735", TipoCliente.PessoaFisica).Value).Value;
        context.Clientes.Add(cliente);

        var daMaria = Seed(tenantId, "b-1", TestPublications.CnjNumberA, Now.AddDays(-1), cliente.Id.Value);
        var deOutro = Seed(tenantId, "b-2", TestPublications.CnjNumberB, Now.AddDays(-1), Guid.NewGuid());
        repository.AddRange([daMaria, deOutro]);
        await context.SaveChangesAsync();

        (await repository.SearchAsync(tenantId, new(Busca: "aparecida"), 0, 10)).Should().ContainSingle().Which.Id.Should().Be(daMaria.Id);
        (await repository.SearchAsync(tenantId, new(Busca: "7654321"), 0, 10)).Should().ContainSingle().Which.Id.Should().Be(deOutro.Id);
        (await repository.SearchAsync(tenantId, new(Busca: "1234567-48"), 0, 10)).Should().ContainSingle().Which.Id.Should().Be(daMaria.Id);
        // Documento: só CPF/CNPJ COMPLETO (índice cego, ADR-068); prefixo não acha, de propósito.
        (await repository.SearchAsync(tenantId, new(Busca: "111.444.777-35"), 0, 10)).Should().ContainSingle().Which.Id.Should().Be(daMaria.Id);
        (await repository.SearchAsync(tenantId, new(Busca: "111.444"), 0, 10)).Should().BeEmpty();
    }

    [Fact]
    public async Task NaoVeAsPublicacoesDeOutroEscritorio()
    {
        var meu = Guid.NewGuid();
        var outro = Guid.NewGuid();
        await using var contextOutro = CreateContext(outro);
        new PublicationRepository(contextOutro).Add(Seed(outro, "x-1", TestPublications.CnjNumberA, Now.AddDays(-1), null));
        await contextOutro.SaveChangesAsync();

        await using var context = CreateContext(meu);

        (await new PublicationRepository(context).SearchAsync(meu, new(), 0, 10)).Should().BeEmpty();
    }

    [Fact]
    public async Task ContadoresDoTopoDaTela()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);
        var hoje = DateOnly.FromDateTime(Now.UtcDateTime);
        var inicioSemana = hoje.AddDays(-2);

        var pendenteHoje = Seed(tenantId, "c-1", TestPublications.CnjNumberA, Now, null);
        var pendenteAntiga = Seed(tenantId, "c-2", TestPublications.CnjNumberA, Now.AddDays(-20), null);
        var tratadaNaSemana = Seed(tenantId, "c-3", TestPublications.CnjNumberA, Now.AddDays(-1), null);
        var tratadaAntiga = Seed(tenantId, "c-4", TestPublications.CnjNumberA, Now.AddDays(-20), null);
        var rejeitada = Seed(tenantId, "c-5", TestPublications.CnjNumberA, Now.AddDays(-1), null);
        tratadaNaSemana.ChangeStatus(PublicationStatus.Approved);
        tratadaAntiga.ChangeStatus(PublicationStatus.Approved);
        rejeitada.ChangeStatus(PublicationStatus.Rejected);
        tratadaNaSemana.MarkAsRead();
        repository.AddRange([pendenteHoje, pendenteAntiga, tratadaNaSemana, tratadaAntiga, rejeitada]);
        await context.SaveChangesAsync();

        var counters = await repository.GetCountersAsync(tenantId, hoje, inicioSemana);

        counters.Total.Should().Be(5);
        counters.PendingReview.Should().Be(2);
        counters.Unread.Should().Be(4);
        counters.PendingToday.Should().Be(1);
        counters.TreatedThisWeek.Should().Be(1, "rejeitada não conta como tratada e a antiga está fora da semana");
    }

    [Fact]
    public async Task ContadoresDeEscritorioVazio_SaoZero()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);

        var counters = await new PublicationRepository(context).GetCountersAsync(tenantId, new(2026, 9, 28), new(2026, 9, 28));

        counters.Should().Be(new PublicationCounters(0, 0, 0, 0, 0));
    }

    private static Publication Seed(Guid tenantId, string externalId, string cnj, DateTimeOffset publishedAt, Guid? clienteId)
    {
        var publication = TestPublications.Create(tenantId, externalId, cnj, publishedAt: publishedAt);
        if (clienteId is { } id)
        {
            publication.LinkTo(Guid.NewGuid(), id, Guid.NewGuid());
        }

        return publication;
    }

    private LegalDbContext CreateContext(Guid tenantId) =>
        TestLegalDbContextFactory.Create(
            fixture.SuperuserConnectionString, TestLegalDbContextFactory.TenantContextFor(tenantId));
}
