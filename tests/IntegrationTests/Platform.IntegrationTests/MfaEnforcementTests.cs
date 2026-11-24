using System.Net;
using System.Net.Http.Headers;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Segundo fator obrigatório para os papéis privilegiados (Security:Mfa): a sessão só vale se o JWT
/// trouxer aal2. O host é derivado do padrão com a exigência LIGADA — no padrão ela fica desligada.
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class MfaEnforcementTests(PlatformApiFactory factory)
{
    private HttpClient EnforcedClient(string role, string? aal)
    {
        var client = factory
            .WithWebHostBuilder(builder => builder.UseSetting("Security:Mfa:Enforce", "true"))
            .CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.JwtTokenFactory.CreateAccessToken(
                Guid.NewGuid(), "socio@teste.com", Guid.NewGuid(), role, assuranceLevel: aal));

        return client;
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Partner")]
    public async Task PrivilegedRole_WithoutSecondFactor_IsAskedForMfa(string role)
    {
        using var client = EnforcedClient(role, aal: "aal1");

        var response = await client.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("AUTH_MFA_REQUIRED");
    }

    [Fact]
    public async Task PrivilegedRole_WithoutTheAalClaimAtAll_IsAskedForMfa()
    {
        using var client = EnforcedClient("Owner", aal: null);

        var response = await client.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PrivilegedRole_WithSecondFactor_IsNotBlockedByMfa()
    {
        using var client = EnforcedClient("Owner", aal: "aal2");

        var response = await client.GetAsync("/api/v1/users");

        (await response.Content.ReadAsStringAsync()).Should().NotContain("AUTH_MFA_REQUIRED");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Lawyer")]
    [InlineData("Intern")]
    public async Task UnprivilegedRole_IsNotRequiredToUseMfa(string role)
    {
        using var client = EnforcedClient(role, aal: "aal1");

        var response = await client.GetAsync("/api/v1/me");

        (await response.Content.ReadAsStringAsync()).Should().NotContain("AUTH_MFA_REQUIRED");
    }

    [Fact]
    public async Task ProfileEndpoint_StaysReachable_SoTheAppCanSendThePartnerToEnrollment()
    {
        using var client = EnforcedClient("Partner", aal: "aal1");

        var response = await client.GetAsync("/api/v1/me");

        (await response.Content.ReadAsStringAsync()).Should().NotContain("AUTH_MFA_REQUIRED");
    }

    [Fact]
    public async Task WhenNotEnforced_NobodyIsAskedForMfa()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.JwtTokenFactory.CreateAccessToken(Guid.NewGuid(), "socio@teste.com", Guid.NewGuid(), "Owner", assuranceLevel: "aal1"));

        var response = await client.GetAsync("/api/v1/users");

        (await response.Content.ReadAsStringAsync()).Should().NotContain("AUTH_MFA_REQUIRED");
    }
}
