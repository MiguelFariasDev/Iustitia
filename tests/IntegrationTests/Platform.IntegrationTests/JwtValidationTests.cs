using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Valida que o JwtBearer do host (ver JwtValidationExtensions/PlatformApiFactory) rejeita
/// tokens ausentes, malformados, com assinatura errada ou expirados — e aceita um token
/// válido assinado pela chave de teste (equivalente à chave que o Supabase exporia via
/// JWKS em produção).
/// </summary>
[Collection(PlatformApiCollection.Name)]
public class JwtValidationTests(PlatformApiFactory factory)
{
    // Endpoint protegido só por Policies.AnyAuthenticatedUser — qualquer papel serve para
    // isolar o comportamento de autenticação (401) do de autorização por papel (403).
    private const string ProtectedUrl = "/api/v1/users/00000000-0000-0000-0000-000000000000";

    [Fact]
    public async Task Request_WithoutAuthorizationHeader_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ProtectedUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithMalformedToken_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "isto-nao-e-um-jwt");

        var response = await client.GetAsync(ProtectedUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithTokenSignedByDifferentKey_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        using var otherRsa = RSA.Create(2048);
        var otherKey = new RsaSecurityKey(otherRsa);

        var token = new JwtSecurityToken(
            audience: "authenticated",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())],
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(otherKey, SecurityAlgorithms.RsaSha256));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        var response = await client.GetAsync(ProtectedUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithExpiredToken_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        var expiredToken = factory.JwtTokenFactory.CreateAccessToken(
            Guid.NewGuid(), "fulano@teste.com", Guid.NewGuid(), "Owner", lifetime: TimeSpan.FromMinutes(-10));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.GetAsync(ProtectedUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithValidToken_AuthenticatesSuccessfully()
    {
        using var client = factory.CreateClient();
        var token = factory.JwtTokenFactory.CreateAccessToken(Guid.NewGuid(), "fulano@teste.com", Guid.NewGuid(), "Owner");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(ProtectedUrl);

        // 404 (usuário inexistente) prova que passou pela autenticação/autorização e chegou
        // ao handler — não 401 (token) nem 403 (papel).
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
