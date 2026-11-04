using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advocacia.Modules.Legal.Application.Features.Clientes;
using Advocacia.Modules.Legal.Application.Features.Processos;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Endpoints de cliente e processo (Legal.Api) pelo host real — mesmo pipeline, mesmo JWT,
/// mesmas policies. Vivem neste projeto porque é aqui que está o PlatformApiFactory.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class ClientesProcessosEndpointsTests(PlatformApiFactory factory)
{
    private const string CnjValido = "1234567-48.2026.8.26.0001";
    private const string CpfValido = "111.444.777-35";

    private HttpClient ClienteAutenticado(Guid tenantId, string role = "Lawyer")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(Guid.NewGuid(), "advogada@teste.com", tenantId, role));

        return client;
    }

    private static CreateClienteCommand NovoCliente(string documento = CpfValido, string nome = "Maria Souza") =>
        new("PessoaFisica", nome, documento);

    private async Task<Guid> CriarClienteAsync(HttpClient client, string documento = CpfValido)
    {
        var response = await client.PostAsJsonAsync("/api/v1/clientes", NovoCliente(documento));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.Content.ReadAsWebJsonAsync<ClienteResponse>())!.Id;
    }

    [Theory]
    [InlineData("/api/v1/clientes")]
    [InlineData("/api/v1/processos")]
    public async Task SemAutenticacao_Retorna401(string rota)
    {
        (await factory.CreateClient().GetAsync(rota)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CadastraCliente_EDevolveODocumentoMascarado()
    {
        var client = ClienteAutenticado(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/v1/clientes", NovoCliente());
        var cliente = await response.Content.ReadAsWebJsonAsync<ClienteResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        cliente!.Nome.Should().Be("Maria Souza");
        cliente.Documento.Should().Be("111.444.777-35");
        cliente.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CadastraCliente_ComCpfInvalido_Retorna400()
    {
        var client = ClienteAutenticado(Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            "/api/v1/clientes", new CreateClienteCommand("PessoaFisica", "Fulano", "111.111.111-11"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CadastraCliente_ComDocumentoRepetido_Retorna409()
    {
        var client = ClienteAutenticado(Guid.NewGuid());
        await CriarClienteAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/clientes", NovoCliente());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task MesmoDocumento_EmOutroEscritorio_EPermitido()
    {
        // Dois escritórios podem atender a mesma pessoa; a unicidade é POR escritório.
        await CriarClienteAsync(ClienteAutenticado(Guid.NewGuid()));

        var response = await ClienteAutenticado(Guid.NewGuid()).PostAsJsonAsync("/api/v1/clientes", NovoCliente());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ListaClientes_NaoVazaOsDeOutroEscritorio()
    {
        var escritorioA = Guid.NewGuid();
        await CriarClienteAsync(ClienteAutenticado(escritorioA));
        await CriarClienteAsync(ClienteAutenticado(Guid.NewGuid()), "529.982.247-25");

        var lista = await (await ClienteAutenticado(escritorioA).GetAsync("/api/v1/clientes"))
            .Content.ReadAsWebJsonAsync<ListClientesResponse>();

        lista!.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task BuscaClientePorNome_Funciona()
    {
        var tenantId = Guid.NewGuid();
        var client = ClienteAutenticado(tenantId);
        await client.PostAsJsonAsync("/api/v1/clientes", NovoCliente(CpfValido, "Joana Prado"));
        await client.PostAsJsonAsync("/api/v1/clientes", NovoCliente("529.982.247-25", "Carlos Menezes"));

        var lista = await (await client.GetAsync("/api/v1/clientes?busca=joana"))
            .Content.ReadAsWebJsonAsync<ListClientesResponse>();

        lista!.Items.Should().ContainSingle().Which.Nome.Should().Be("Joana Prado");
    }

    [Fact]
    public async Task DesativarCliente_ExigePartnerOuAcima()
    {
        var tenantId = Guid.NewGuid();
        var lawyer = ClienteAutenticado(tenantId, "Lawyer");
        var clienteId = await CriarClienteAsync(lawyer);

        (await lawyer.DeleteAsync($"/api/v1/clientes/{clienteId}")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        (await ClienteAutenticado(tenantId, "Partner").DeleteAsync($"/api/v1/clientes/{clienteId}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CadastraProcesso_VinculadoAoCliente()
    {
        var client = ClienteAutenticado(Guid.NewGuid());
        var clienteId = await CriarClienteAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/processos", new CreateProcessoCommand(CnjValido, clienteId, "Civel"));

        var processo = await response.Content.ReadAsWebJsonAsync<ProcessoResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        processo!.CnjNumber.Should().Be(CnjValido);
        processo.ClienteId.Should().Be(clienteId);
        processo.Status.Should().Be("Ativo");

        // É o que faz a captura acontecer para este processo.
        processo.GeraCaptura.Should().BeTrue();
    }

    [Fact]
    public async Task CadastraProcesso_ComClienteInexistente_Retorna404()
    {
        var client = ClienteAutenticado(Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            "/api/v1/processos", new CreateProcessoCommand(CnjValido, Guid.NewGuid(), "Civel"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CadastraProcesso_ComCnjInvalido_Retorna400()
    {
        var client = ClienteAutenticado(Guid.NewGuid());
        var clienteId = await CriarClienteAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/processos", new CreateProcessoCommand("1234567-49.2026.8.26.0001", clienteId, "Civel"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CadastraProcesso_ComCnjRepetido_Retorna409()
    {
        var client = ClienteAutenticado(Guid.NewGuid());
        var clienteId = await CriarClienteAsync(client);
        await client.PostAsJsonAsync("/api/v1/processos", new CreateProcessoCommand(CnjValido, clienteId, "Civel"));

        var response = await client.PostAsJsonAsync(
            "/api/v1/processos", new CreateProcessoCommand(CnjValido, clienteId, "Trabalhista"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ArquivarProcesso_InterrompeACaptura()
    {
        var tenantId = Guid.NewGuid();
        var client = ClienteAutenticado(tenantId);
        var clienteId = await CriarClienteAsync(client);

        var criado = await (await client.PostAsJsonAsync(
            "/api/v1/processos", new CreateProcessoCommand(CnjValido, clienteId, "Civel")))
            .Content.ReadAsWebJsonAsync<ProcessoResponse>();

        var arquivar = await ClienteAutenticado(tenantId, "Partner")
            .PostAsync($"/api/v1/processos/{criado!.Id}/archive", content: null);

        arquivar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var depois = await (await client.GetAsync($"/api/v1/processos/{criado.Id}"))
            .Content.ReadAsWebJsonAsync<ProcessoResponse>();

        depois!.Status.Should().Be("Arquivado");
        depois.GeraCaptura.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessoDeOutroEscritorio_Retorna404()
    {
        var client = ClienteAutenticado(Guid.NewGuid());
        var clienteId = await CriarClienteAsync(client);
        var criado = await (await client.PostAsJsonAsync(
            "/api/v1/processos", new CreateProcessoCommand(CnjValido, clienteId, "Civel")))
            .Content.ReadAsWebJsonAsync<ProcessoResponse>();

        var outro = await ClienteAutenticado(Guid.NewGuid()).GetAsync($"/api/v1/processos/{criado!.Id}");

        outro.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
