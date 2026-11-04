using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.Application.Features.Users.ListUsers;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Verifica, via HTTP real (não só a nível de repositório — ver TenantIsolationTests em
/// Etapa 0.2), que o tenant_id da claim do JWT (Custom Access Token Hook, ADR-029),
/// propagado por TenantContextMiddleware para ITenantContext, é o que decide o isolamento
/// entre escritórios — tanto pelo filtro global do EF Core quanto pelo RLS por trás dele
/// (ADR-004), e que os handlers de Tenant (que não têm esse filtro automático, por serem a
/// própria fronteira de tenant) validam a posse do recurso explicitamente.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class RlsWithJwtClaimsTests(PlatformApiFactory factory)
{
    private async Task<(Guid TenantId, Guid OwnerId)> CreateTenantAsync(HttpClient client)
    {
        var command = new CreateTenantCommand(
            "Escritorio Teste", TestCnpjGenerator.Generate(), "Owner Teste",
            $"owner-{Guid.NewGuid():N}@teste.com", "Senha123!");

        var response = await client.PostIdempotentAsJsonAsync(
            "/api/v1/tenants", command);
        var created = await response.Content.ReadAsWebJsonAsync<CreateTenantResponse>();

        return (created!.TenantId, created.AdminUserId);
    }

    private void AuthenticateAsOwner(HttpClient client, Guid userId, Guid tenantId) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(userId, "qualquer@teste.com", tenantId, "Owner"));

    [Fact]
    public async Task GetUser_FromAnotherTenant_ReturnsNotFound()
    {
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();

        var (tenantIdA, ownerIdA) = await CreateTenantAsync(clientA);
        var (_, ownerIdB) = await CreateTenantAsync(clientB);

        AuthenticateAsOwner(clientA, ownerIdA, tenantIdA);

        // Owner de A tentando ler o Owner de B pelo Id — o filtro global de tenant (User
        // implementa IHasTenant) deve esconder a linha como se ela não existisse, nunca
        // vazar "existe, mas não é seu".
        var response = await clientA.GetAsync($"/api/v1/users/{ownerIdB}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListUsers_OnlyReturnsUsersFromOwnTenant()
    {
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();

        var (tenantIdA, ownerIdA) = await CreateTenantAsync(clientA);
        var (_, ownerIdB) = await CreateTenantAsync(clientB);

        AuthenticateAsOwner(clientA, ownerIdA, tenantIdA);

        var response = await clientA.GetAsync("/api/v1/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await response.Content.ReadAsWebJsonAsync<ListUsersResponse>();
        list.Should().NotBeNull();
        list!.Items.Should().Contain(u => u.Id == ownerIdA);
        list.Items.Should().NotContain(u => u.Id == ownerIdB);
    }

    [Fact]
    public async Task GetTenant_FromAnotherTenant_ReturnsNotFound()
    {
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();

        var (tenantIdA, ownerIdA) = await CreateTenantAsync(clientA);
        var (tenantIdB, _) = await CreateTenantAsync(clientB);

        AuthenticateAsOwner(clientA, ownerIdA, tenantIdA);

        // Tenant não implementa IHasTenant (é a própria fronteira de multi-tenancy) e
        // Policies.OwnerOnly só valida o papel, não a posse do recurso — GetTenantByIdHandler
        // precisa checar explicitamente que o :id da rota é o tenant do próprio token (ver
        // ADR-028/comentário no handler). Owner de A não pode ler os dados do tenant B.
        var response = await clientA.GetAsync($"/api/v1/tenants/{tenantIdB}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTenant_FromOwnTenant_ReturnsOk()
    {
        using var client = factory.CreateClient();
        var (tenantId, ownerId) = await CreateTenantAsync(client);
        AuthenticateAsOwner(client, ownerId, tenantId);

        var response = await client.GetAsync($"/api/v1/tenants/{tenantId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
