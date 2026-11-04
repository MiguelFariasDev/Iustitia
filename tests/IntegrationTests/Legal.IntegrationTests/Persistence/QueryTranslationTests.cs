using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Specifications;
using Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Legal.IntegrationTests.Common;
using FluentAssertions;

namespace Advocacia.Modules.Legal.IntegrationTests.Persistence;

/// <summary>
/// Toda consulta dos repositórios precisa TRADUZIR para SQL. Parece óbvio, mas esta classe
/// de erro não aparece em compilação nem em teste com dublê: só quando o EF Core tenta
/// montar o comando contra um banco real.
///
/// Dois modos de falha, os dois encontrados de verdade ao construir esta etapa:
///   1. `vo.Value.Contains(x)` sobre um value object com HasConversion — o tradutor não
///      alcança um membro dentro de um valor convertido ("could not be translated").
///   2. `EF.Property&lt;string&gt;(e, "Nome")` sobre a mesma propriedade — pior ainda: o EF
///      aplica o CONVERSOR ao parâmetro e tenta transformar o termo digitado no value
///      object, estourando InvalidCastException em runtime.
///
/// A correção foi expor colunas derivadas (NomeBusca, DocumentoBusca, CnjDigits). Estes
/// testes existem para que ninguém volte a apontar a busca para o value object.
/// </summary>
[Collection(LegalPostgresCollection.Name)]
public class QueryTranslationTests(LegalPostgresFixture fixture)
{
    [Fact]
    public async Task ListagemDeClientesComBusca_TraduzParaSql()
    {
        var tenantId = Guid.NewGuid();
        await using var context = TestLegalDbContextFactory.Create(
            fixture.SuperuserConnectionString, TestLegalDbContextFactory.TenantContextFor(tenantId));

        var repository = new ClienteRepository(context);

        var act = async () => await repository.ListAsync(tenantId, null, null, "silva", 0, 10);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ContagemDeClientesComBusca_TraduzParaSql()
    {
        var tenantId = Guid.NewGuid();
        await using var context = TestLegalDbContextFactory.Create(
            fixture.SuperuserConnectionString, TestLegalDbContextFactory.TenantContextFor(tenantId));

        var act = async () => await new ClienteRepository(context).CountAsync(tenantId, null, null, "silva");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConsultasDeProcesso_TraduzemParaSql()
    {
        var tenantId = Guid.NewGuid();
        await using var context = TestLegalDbContextFactory.Create(
            fixture.SuperuserConnectionString, TestLegalDbContextFactory.TenantContextFor(tenantId));

        var repository = new ProcessoRepository(context);

        var act = async () =>
        {
            await repository.ListAsync(tenantId, null, null, null, null, "0000777", null, null, null, 0, 10);
            await repository.ListAsync(tenantId, null, null, null, null, "prefeitura", "TJCE", new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), 0, 10);
            await repository.GetByCnjAsync(tenantId, "0000777-07.2018.8.06.0100");
            await repository.ListParaCapturaAsync(tenantId);
            await repository.ListTenantsComProcessosAtivosAsync();
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BuscaPorDocumento_TraduzParaSql()
    {
        var tenantId = Guid.NewGuid();
        await using var context = TestLegalDbContextFactory.Create(
            fixture.SuperuserConnectionString, TestLegalDbContextFactory.TenantContextFor(tenantId));

        var repository = new ClienteRepository(context);
        var documento = Documento.Create("11144477735", TipoCliente.PessoaFisica).Value;

        var act = async () => await repository.GetByDocumentoAsync(tenantId, documento);

        await act.Should().NotThrowAsync();
    }
}
