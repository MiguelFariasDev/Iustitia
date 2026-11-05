using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advocacia.Modules.Legal.Application.Features.Clientes;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Npgsql;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// A trilha de auditoria pelo host real: ações de Legal são gravadas com quem/quando/IP/User-Agent
/// e SEM cópia dos dados pessoais (Etapa 3.9, ADR-069).
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class AuditTrailEndpointsTests(PlatformApiFactory factory)
{
    private const string Cpf = "111.444.777-35";

    private (HttpClient Client, Guid UserId, Guid Tenant) Authenticated()
    {
        var tenant = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(userId, "advogada@teste.com", tenant, "Lawyer"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IustitiaTests/1.0");

        return (client, userId, tenant);
    }

    private async Task<List<(string Action, string EntityType, Guid UserId, string? UserAgent, string? After)>> AuditOfAsync(string entityId)
    {
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT action, entity_type, user_id, user_agent, after::text FROM audit_logs WHERE entity_id = @e ORDER BY created_at";
        command.Parameters.AddWithValue("e", entityId);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<(string, string, Guid, string?, string?)>();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetString(0), reader.GetString(1), reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return rows;
    }

    [Fact]
    public async Task CreateUpdateViewDeactivate_ClienteIsAuditedInOrder_WithActorAndUserAgent_AndNoPersonalData()
    {
        var (client, userId, _) = Authenticated();

        var created = await (await client.PostAsJsonAsync(
            "/api/v1/clientes",
            new CreateClienteCommand("PessoaFisica", "Maria Souza", Cpf, new ContatoRequest("maria@exemplo.com.br", "(11) 98765-4321"))))
            .Content.ReadAsWebJsonAsync<ClienteResponse>();
        var id = created!.Id;

        (await client.GetAsync($"/api/v1/clientes/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/api/v1/clientes/{id}", new { Nome = "Maria S. Souza" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/v1/clientes")).StatusCode.Should().Be(HttpStatusCode.OK); // listagem NÃO é "Viewed"

        var trail = await AuditOfAsync(id.ToString());

        trail.Select(row => row.Action).Should().Equal("Created", "Viewed", "Updated");
        trail.Should().OnlyContain(row => row.EntityType == "Cliente" && row.UserId == userId);
        trail.Should().OnlyContain(row => row.UserAgent == "IustitiaTests/1.0");

        // Nenhum dado pessoal copiado para a tabela imutável.
        trail.Should().OnlyContain(row => row.After == null);
    }

    [Fact]
    public async Task FailedRequests_LeaveNoAuditTrail()
    {
        var (client, _, _) = Authenticated();
        var missing = Guid.NewGuid();

        (await client.GetAsync($"/api/v1/clientes/{missing}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await AuditOfAsync(missing.ToString())).Should().BeEmpty();
    }
}
