using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advocacia.Modules.Legal.Application.Features.Clientes;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Criptografia de campos pessoais em repouso (Etapa 3.9, ADR-068) pelo host real: o que a API
/// devolve continua legível, mas o que está GRAVADO no Postgres não é. A leitura do banco é por
/// SQL cru, conexão própria — é isso que um atacante com acesso ao banco (ou um backup vazado)
/// enxergaria.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class FieldEncryptionTests(PlatformApiFactory factory)
{
    private const string Cpf = "111.444.777-35";
    private const string CpfDigitos = "11144477735";

    private HttpClient ClienteAutenticado(Guid tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(Guid.NewGuid(), "advogada@teste.com", tenantId, "Lawyer"));

        return client;
    }

    private async Task<ClienteResponse> CriarAsync(
        HttpClient client, string documento = Cpf, string? email = "maria@exemplo.com.br", string? telefone = "(11) 98765-4321")
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/clientes",
            new CreateClienteCommand("PessoaFisica", "Maria Souza", documento, new ContatoRequest(email, telefone)));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.Content.ReadAsWebJsonAsync<ClienteResponse>())!;
    }

    private async Task<(string Documento, string DocumentoBusca, string? Email, string? Telefone)> LerLinhaCrua(Guid clienteId)
    {
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT documento, documento_busca, email, telefone FROM clientes WHERE id = @id";
        command.Parameters.AddWithValue("id", clienteId);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        return (
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    [Fact]
    public async Task Cliente_NoBanco_TemDocumentoEmailETelefoneCifrados_SemNenhumTextoPuro()
    {
        var cliente = await CriarAsync(ClienteAutenticado(Guid.NewGuid()));

        var linha = await LerLinhaCrua(cliente.Id);

        linha.Documento.Should().StartWith("enc:v1:").And.NotContain(CpfDigitos);
        linha.Email.Should().StartWith("enc:v1:").And.NotContain("maria@exemplo");
        linha.Telefone.Should().StartWith("enc:v1:").And.NotContain("98765");

        // A coluna de busca é um HMAC: sem o CPF, sem prefixo do CPF, sem nada reversível.
        linha.DocumentoBusca.Should().MatchRegex("^[0-9a-f]{64}$");
        linha.DocumentoBusca.Should().NotContain(CpfDigitos).And.NotContain(CpfDigitos[..6]);
    }

    [Fact]
    public async Task Api_DevolveOsDadosDecifradosParaOUsuarioAutorizado()
    {
        var tenant = Guid.NewGuid();
        var client = ClienteAutenticado(tenant);
        var criado = await CriarAsync(client);

        var lido = await (await client.GetAsync($"/api/v1/clientes/{criado.Id}")).Content.ReadAsWebJsonAsync<ClienteResponse>();

        lido!.Documento.Should().Be(Cpf);
        lido.Email.Should().Be("maria@exemplo.com.br");
        lido.Telefone.Should().Be("(11) 98765-4321");
    }

    [Fact]
    public async Task DoisClientes_ComOMesmoCpf_NaoCompartilhamTextoCifrado()
    {
        // Prova a não-determinismo: o mesmo CPF em dois escritórios gera valores cifrados
        // diferentes — quem vê o banco não descobre quem é cliente de mais de um escritório.
        var a = await CriarAsync(ClienteAutenticado(Guid.NewGuid()));
        var b = await CriarAsync(ClienteAutenticado(Guid.NewGuid()));

        var linhaA = await LerLinhaCrua(a.Id);
        var linhaB = await LerLinhaCrua(b.Id);

        linhaA.Documento.Should().NotBe(linhaB.Documento);
    }

    [Fact]
    public async Task UnicidadeDoDocumento_ContinuaValendoSobreOIndiceCego()
    {
        var client = ClienteAutenticado(Guid.NewGuid());
        await CriarAsync(client);

        var duplicado = await client.PostAsJsonAsync(
            "/api/v1/clientes", new CreateClienteCommand("PessoaFisica", "Outra Maria", Cpf));

        duplicado.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task BuscaPorCpfCompleto_EncontraOCliente_PelaIgualdadeDoIndiceCego()
    {
        var tenant = Guid.NewGuid();
        var client = ClienteAutenticado(tenant);
        var criado = await CriarAsync(client);

        var porFormatado = await (await client.GetAsync($"/api/v1/clientes?busca={Uri.EscapeDataString(Cpf)}"))
            .Content.ReadAsWebJsonAsync<ListClientesResponse>();
        var porDigitos = await (await client.GetAsync($"/api/v1/clientes?busca={CpfDigitos}"))
            .Content.ReadAsWebJsonAsync<ListClientesResponse>();

        porFormatado!.Items.Should().ContainSingle(c => c.Id == criado.Id);
        porDigitos!.Items.Should().ContainSingle(c => c.Id == criado.Id);
    }

    [Fact]
    public async Task BuscaPorPrefixoDoCpf_NaoEncontraNada_PorDesenho()
    {
        // O prefixo em claro anularia a criptografia (ver ADR-068): só o documento inteiro busca.
        var client = ClienteAutenticado(Guid.NewGuid());
        await CriarAsync(client);

        var lista = await (await client.GetAsync("/api/v1/clientes?busca=111444"))
            .Content.ReadAsWebJsonAsync<ListClientesResponse>();

        lista!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ClienteLegado_EmTextoPuro_ContinuaLegivel_EmigraAoSerAtualizado()
    {
        // Linha gravada ANTES da Etapa 3.9 (texto puro, documento_busca em claro): a leitura
        // aceita o legado enquanto Encryption:AllowLegacyPlaintext=true.
        var tenant = Guid.NewGuid();
        var client = ClienteAutenticado(tenant);
        var criado = await CriarAsync(client, documento: "529.982.247-25", email: "legado@exemplo.com", telefone: null);

        await using (var connection = new NpgsqlConnection(factory.DatabaseConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE clientes SET documento = '52998224725', documento_busca = '52998224725', email = 'legado@exemplo.com' WHERE id = @id";
            command.Parameters.AddWithValue("id", criado.Id);
            await command.ExecuteNonQueryAsync();
        }

        var lido = await (await client.GetAsync($"/api/v1/clientes/{criado.Id}")).Content.ReadAsWebJsonAsync<ClienteResponse>();

        lido!.Documento.Should().Be("529.982.247-25");
        lido.Email.Should().Be("legado@exemplo.com");
    }
}

[Collection(PlatformApiCollection.Name)]
public class FieldEncryptionBackfillTests(PlatformApiFactory factory)
{
    [Fact]
    public async Task Backfill_EncryptsLegacyRows_RebuildsTheBlindIndex_AndIsIdempotent()
    {
        var tenant = Guid.NewGuid();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(Guid.NewGuid(), "advogada@teste.com", tenant, "Lawyer"));

        var response = await client.PostAsJsonAsync(
            "/api/v1/clientes",
            new CreateClienteCommand("PessoaFisica", "Cliente Legado", "390.533.447-05", new ContatoRequest("legado@exemplo.com", "(21) 99999-0000")));
        var criado = (await response.Content.ReadAsWebJsonAsync<ClienteResponse>())!;

        // Simula a linha como ela estava antes da Etapa 3.9.
        await using (var connection = new NpgsqlConnection(factory.DatabaseConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE clientes
                SET documento = '39053344705', documento_busca = '39053344705',
                    email = 'legado@exemplo.com', telefone = '21999990000'
                WHERE id = @id
                """;
            command.Parameters.AddWithValue("id", criado.Id);
            await command.ExecuteNonQueryAsync();
        }

        int processed;
        using (var scope = factory.Services.CreateScope())
        {
            processed = await scope.ServiceProvider
                .GetRequiredService<Advocacia.Modules.Legal.Infrastructure.Jobs.ClienteEncryptionBackfillJob>()
                .RunAsync(CancellationToken.None);
        }

        processed.Should().BeGreaterThanOrEqualTo(1);

        await using (var connection = new NpgsqlConnection(factory.DatabaseConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT documento, documento_busca, email, telefone FROM clientes WHERE id = @id";
            command.Parameters.AddWithValue("id", criado.Id);
            await using var reader = await command.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();

            reader.GetString(0).Should().StartWith("enc:v1:").And.NotContain("39053344705");
            reader.GetString(1).Should().MatchRegex("^[0-9a-f]{64}$");
            reader.GetString(2).Should().StartWith("enc:v1:");
            reader.GetString(3).Should().StartWith("enc:v1:");
        }

        // Depois do backfill o cliente é achado pelo CPF e a duplicidade volta a ser detectada.
        var busca = await (await client.GetAsync("/api/v1/clientes?busca=39053344705")).Content.ReadAsWebJsonAsync<ListClientesResponse>();
        busca!.Items.Should().ContainSingle(c => c.Id == criado.Id);

        (await client.PostAsJsonAsync("/api/v1/clientes", new CreateClienteCommand("PessoaFisica", "Duplicado", "390.533.447-05")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var secondScope = factory.Services.CreateScope();
        (await secondScope.ServiceProvider
            .GetRequiredService<Advocacia.Modules.Legal.Infrastructure.Jobs.ClienteEncryptionBackfillJob>()
            .RunAsync(CancellationToken.None)).Should().Be(0, "o backfill só toca linhas que ainda precisam");
    }
}
