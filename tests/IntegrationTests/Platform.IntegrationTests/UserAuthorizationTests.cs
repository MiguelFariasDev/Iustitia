using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.Application.Features.Users.InviteUser;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Valida o RBAC por policies (ADR-028): a claim "user_role" do JWT decide quem pode
/// executar cada ação, independente do estado local do usuário — [Authorize(Roles=...)]
/// nega a requisição antes mesmo de o handler rodar.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class UserAuthorizationTests(PlatformApiFactory factory)
{
    private async Task<(Guid TenantId, string OwnerEmail, string OwnerPassword)> CreateTenantAsync(HttpClient client)
    {
        var command = new CreateTenantCommand(
            "Escritorio Teste", TestCnpjGenerator.Generate(), "Owner Teste",
            $"owner-{Guid.NewGuid():N}@teste.com", "Senha123!");

        var response = await client.PostIdempotentAsJsonAsync(
            "/api/v1/tenants", command);
        var created = await response.Content.ReadAsWebJsonAsync<CreateTenantResponse>();

        return (created!.TenantId, command.AdminEmail, command.AdminPassword);
    }

    private void AuthenticateAs(HttpClient client, Guid tenantId, string role) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(Guid.NewGuid(), "qualquer@teste.com", tenantId, role));

    [Theory]
    [InlineData("Owner")]
    [InlineData("Partner")]
    [InlineData("Lawyer")]
    [InlineData("Intern")]
    [InlineData("Admin")]
    [InlineData("Financial")]
    public async Task GetTenant_WithAnyAuthenticatedRole_ReturnsOk(string role)
    {
        // GetTenantById é AnyAuthenticatedUser desde a Etapa 0.7 (ver ADR-039/policies
        // revisadas): ver os próprios dados do escritório é uma leitura, não uma ação
        // administrativa — restringi-la a Owner era desnecessariamente restritivo.
        using var client = factory.CreateClient();
        var (tenantId, _, _) = await CreateTenantAsync(client);
        AuthenticateAs(client, tenantId, role);

        var response = await client.GetAsync($"/api/v1/tenants/{tenantId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Partner")]
    public async Task InviteUser_WithPartnerOrAbove_ReturnsOk(string role)
    {
        using var client = factory.CreateClient();
        var (tenantId, _, _) = await CreateTenantAsync(client);
        AuthenticateAs(client, tenantId, role);

        var response = await client.PostIdempotentAsJsonAsync(
            "/api/v1/users/invite",
            new InviteUserCommand($"convidado-{Guid.NewGuid():N}@teste.com", "Convidado", "Lawyer", null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Lawyer")]
    [InlineData("Intern")]
    public async Task InviteUser_WithRoleBelowPartner_ReturnsForbidden(string role)
    {
        using var client = factory.CreateClient();
        var (tenantId, _, _) = await CreateTenantAsync(client);
        AuthenticateAs(client, tenantId, role);

        var response = await client.PostIdempotentAsJsonAsync(
            "/api/v1/users/invite",
            new InviteUserCommand($"convidado-{Guid.NewGuid():N}@teste.com", "Convidado", "Lawyer", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnyAuthenticatedEndpoint_WithAnyValidRole_IsNotForbidden()
    {
        using var client = factory.CreateClient();
        var (tenantId, _, _) = await CreateTenantAsync(client);
        AuthenticateAs(client, tenantId, "Intern");

        var response = await client.GetAsync($"/api/v1/users/{Guid.NewGuid()}");

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
