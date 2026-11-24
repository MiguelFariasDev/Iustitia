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

    [Fact]
    public async Task Listagem_restrita_omite_confidenciais_de_outro_responsavel()
    {
        var tenantId = Guid.NewGuid();
        var advogada = Guid.NewGuid();
        await using var context = TestLegalDbContextFactory.Create(
            fixture.SuperuserConnectionString, TestLegalDbContextFactory.TenantContextFor(tenantId));
        var repository = new ProcessoRepository(context);

        Processo Novo(string cnj, Guid responsavel, bool confidencial)
        {
            var processo = Processo.Create(
                tenantId, CNJNumber.Create(cnj).Value, Guid.NewGuid(), responsavel, AreaDireito.Civel).Value;
            processo.DefinirConfidencialidade(confidencial);

            return processo;
        }

        var comum = Novo(TestPublications.CnjNumberA, Guid.NewGuid(), confidencial: false);
        var sigiloDeOutro = Novo(TestPublications.CnjNumberB, Guid.NewGuid(), confidencial: true);
        var sigiloProprio = Novo(TestPublications.CnjNumberC, advogada, confidencial: true);
        repository.Add(comum);
        repository.Add(sigiloDeOutro);
        repository.Add(sigiloProprio);
        await context.SaveChangesAsync();

        var daAdvogada = await repository.ListAsync(
            tenantId, null, null, null, null, null, null, null, null, 0, 20, restritoAoUsuario: advogada);
        var contagem = await repository.CountAsync(
            tenantId, null, null, null, null, null, null, null, null, restritoAoUsuario: advogada);
        var daSociedade = await repository.ListAsync(
            tenantId, null, null, null, null, null, null, null, null, 0, 20);

        daAdvogada.Select(p => p.Id).Should().BeEquivalentTo([comum.Id, sigiloProprio.Id]);
        contagem.Should().Be(2);
        daSociedade.Should().HaveCount(3);
    }
}
