using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.IntegrationTests.Common;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;
using FluentAssertions;

namespace Advocacia.Modules.Legal.IntegrationTests.Persistence;

[Collection(LegalPostgresCollection.Name)]
public class ProcessoRepositoryFiltrosTests(LegalPostgresFixture fixture)
{
    [Fact]
    public async Task Filtra_por_tribunal_e_por_periodo_de_distribuicao()
    {
        var tenantId = Guid.NewGuid();
        await using var context = TestLegalDbContextFactory.Create(
            fixture.SuperuserConnectionString, TestLegalDbContextFactory.TenantContextFor(tenantId));
        var repository = new ProcessoRepository(context);

        Processo Novo(string cnj, string tribunal, DateOnly distribuicao) => Processo.Create(
            tenantId, CNJNumber.Create(cnj).Value, Guid.NewGuid(), Guid.NewGuid(), AreaDireito.Civel,
            DadosForo.Create(tribunal, null, null).Value, dataDistribuicao: distribuicao).Value;

        var ce = Novo(TestPublications.CnjNumberA, "tjce", new DateOnly(2024, 3, 10));
        var pi = Novo(TestPublications.CnjNumberB, "TJPI", new DateOnly(2025, 6, 1));
        repository.Add(ce);
        repository.Add(pi);
        await context.SaveChangesAsync();

        var soCe = await repository.ListAsync(
            tenantId, null, null, null, null, null, "TJCE", null, null, 0, 20);
        soCe.Select(p => p.Id).Should().BeEquivalentTo([ce.Id]);

        var aPartirDe2025 = await repository.CountAsync(
            tenantId, null, null, null, null, null, null, new DateOnly(2025, 1, 1), null);
        aPartirDe2025.Should().Be(1);

        var ateFim2024 = await repository.ListAsync(
            tenantId, null, null, null, null, null, null, null, new DateOnly(2024, 12, 31), 0, 20);
        ateFim2024.Select(p => p.Id).Should().BeEquivalentTo([ce.Id]);
    }
}
