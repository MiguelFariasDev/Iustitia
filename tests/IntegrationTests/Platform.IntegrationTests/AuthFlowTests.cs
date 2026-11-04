using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advocacia.Platform.Application.Features.Auth.Login;
using Advocacia.Platform.Application.Features.Auth.Logout;
using Advocacia.Platform.Application.Features.Auth.RefreshToken;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.Application.Features.Users.AcceptInvite;
using Advocacia.Platform.Application.Features.Users.InviteUser;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Exercita os fluxos de autenticação descritos em docs/compliance/auth-flow.md de ponta
/// a ponta via HTTP real contra o host de Platform.Api — cadastro de escritório, login,
/// refresh, logout, convite e aceite de convite.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class AuthFlowTests(PlatformApiFactory factory)
{
    private HttpClient CreateClient() => factory.CreateClient();

    private static CreateTenantCommand NewTenantCommand(string emailSuffix) => new(
        Name: "Escritorio Teste",
        Cnpj: TestCnpjGenerator.Generate(),
        AdminName: "Owner Teste",
        AdminEmail: $"owner-{emailSuffix}@teste.com",
        AdminPassword: "Senha123!");

    /// <summary>
    /// Projeto Supabase com confirmação de e-mail ligada: o signup não devolve sessão. O
    /// escritório precisa nascer assim mesmo, com a resposta sinalizando a confirmação
    /// pendente. Antes da correção este caminho devolvia 500 e deixava a conta órfã no
    /// Supabase — bloqueando a nova tentativa com "e-mail já cadastrado".
    /// </summary>
    [Fact]
    public async Task RegisterTenant_WhenEmailConfirmationRequired_CreatesTenantAnyway()
    {
        using var client = CreateClient();
        var command = NewTenantCommand(Guid.NewGuid().ToString("N"));

        factory.SupabaseAuthService.RequiresEmailConfirmation = true;
        try
        {
            var createResponse = await client.PostIdempotentAsJsonAsync("/api/v1/tenants", command);

            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = await createResponse.Content.ReadAsWebJsonAsync<CreateTenantResponse>();
            created.Should().NotBeNull();
            created!.RequiresEmailConfirmation.Should().BeTrue();
            created.TenantId.Should().NotBeEmpty();

            // A conta continua existindo: a compensação só dispara quando o lado local falha.
            factory.SupabaseAuthService.HasUser(command.AdminEmail).Should().BeTrue();
        }
        finally
        {
            factory.SupabaseAuthService.RequiresEmailConfirmation = false;
        }
    }

    [Fact]
    public async Task RegisterTenant_ThenLogin_ReturnsSessionForOwner()
    {
        using var client = CreateClient();
        var command = NewTenantCommand(Guid.NewGuid().ToString("N"));

        var createResponse = await client.PostIdempotentAsJsonAsync(
            "/api/v1/tenants", command);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadAsWebJsonAsync<CreateTenantResponse>();
        created.Should().NotBeNull();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginCommand(command.AdminEmail, command.AdminPassword));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await loginResponse.Content.ReadAsWebJsonAsync<LoginResponse>();
        login.Should().NotBeNull();
        login!.TenantId.Should().Be(created!.TenantId);
        login.Role.Should().Be("Owner");
        login.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        var command = NewTenantCommand(Guid.NewGuid().ToString("N"));
        await client.PostIdempotentAsJsonAsync(
            "/api/v1/tenants", command);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginCommand(command.AdminEmail, "senha-errada"));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_WithSessionFromLogin_ReturnsNewAccessToken()
    {
        using var client = CreateClient();
        var command = NewTenantCommand(Guid.NewGuid().ToString("N"));
        await client.PostIdempotentAsJsonAsync(
            "/api/v1/tenants", command);
        var login = await (await client.PostAsJsonAsync(
                "/api/v1/auth/login", new LoginCommand(command.AdminEmail, command.AdminPassword)))
            .Content.ReadAsWebJsonAsync<LoginResponse>();

        var refreshResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new RefreshTokenCommand(login!.RefreshToken));

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await refreshResponse.Content.ReadAsWebJsonAsync<RefreshTokenResponse>();
        refreshed.Should().NotBeNull();
        refreshed!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Logout_WithAuthenticatedUser_Succeeds()
    {
        using var client = CreateClient();
        var command = NewTenantCommand(Guid.NewGuid().ToString("N"));
        var created = await (await client.PostIdempotentAsJsonAsync(
            "/api/v1/tenants", command))
            .Content.ReadAsWebJsonAsync<CreateTenantResponse>();

        var token = factory.JwtTokenFactory.CreateAccessToken(created!.AdminUserId, command.AdminEmail, created.TenantId, "Owner");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var logoutResponse = await client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutCommand("qualquer-token"));

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutCommand("qualquer-token"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InviteUser_ThenAcceptInvite_ActivatesInvitedUser()
    {
        using var client = CreateClient();
        var ownerCommand = NewTenantCommand(Guid.NewGuid().ToString("N"));
        var created = await (await client.PostIdempotentAsJsonAsync(
            "/api/v1/tenants", ownerCommand))
            .Content.ReadAsWebJsonAsync<CreateTenantResponse>();

        var ownerToken = factory.JwtTokenFactory.CreateAccessToken(
            created!.AdminUserId, ownerCommand.AdminEmail, created.TenantId, "Owner");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var inviteeEmail = $"convidado-{Guid.NewGuid():N}@teste.com";
        var inviteResponse = await client.PostIdempotentAsJsonAsync(
            "/api/v1/users/invite", new InviteUserCommand(inviteeEmail, "Convidado Teste", "Lawyer", "Cível"));

        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var invited = await inviteResponse.Content.ReadAsWebJsonAsync<InviteUserResponse>();
        invited.Should().NotBeNull();

        // Simula o SDK client-side trocando o link de convite por uma sessão provisória —
        // no fake, o access_token do usuário convidado é só o próprio Id (ver
        // FakeSupabaseAuthService).
        using var anonymousClient = CreateClient();
        var acceptResponse = await anonymousClient.PostIdempotentAsJsonAsync(
            "/api/v1/users/accept-invite",
            new AcceptInviteCommand(invited!.UserId.ToString(), "NovaSenha123!", "Convidado Confirmado"));

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await anonymousClient.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginCommand(inviteeEmail, "NovaSenha123!"));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await loginResponse.Content.ReadAsWebJsonAsync<LoginResponse>();
        login!.Role.Should().Be("Lawyer");
    }
}
