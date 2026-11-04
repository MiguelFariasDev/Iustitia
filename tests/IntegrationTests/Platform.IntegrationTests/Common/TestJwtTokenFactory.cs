using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Advocacia.Platform.IntegrationTests.Common;

/// <summary>
/// Emite JWTs assinados por uma chave RSA local (gerada em memória, descartada com o
/// processo de teste) — substitui, nos testes de integração, o par de chaves real que o
/// Supabase exporia via JWKS. O host de teste (ver PlatformApiFactory) valida usando a
/// chave pública desta mesma instância (<see cref="SecurityKey"/>), via
/// TokenValidationParameters.IssuerSigningKey, sem nenhuma chamada de rede.
/// </summary>
public sealed class TestJwtTokenFactory : IDisposable
{
    private readonly RSA _rsa = RSA.Create(2048);

    public RsaSecurityKey SecurityKey { get; }

    public TestJwtTokenFactory() => SecurityKey = new RsaSecurityKey(_rsa);

    /// <summary>
    /// Monta um JWT com o mesmo shape que o Custom Access Token Hook do Supabase produziria
    /// (ver infra/supabase/migrations/004_custom_access_token_hook.sql): "sub"/"email" padrão
    /// do Supabase, "tenant_id" e "user_role" (nunca "role", reservado — ver ADR-029)
    /// injetados pelo hook.
    /// </summary>
    public string CreateAccessToken(Guid userId, string email, Guid? tenantId, string? role, TimeSpan? lifetime = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("email", email),
            new("role", "authenticated"), // claim reservada do Supabase — nunca o papel de negócio.
        };

        if (tenantId is { } tenant)
        {
            claims.Add(new Claim("tenant_id", tenant.ToString()));
        }

        if (role is not null)
        {
            claims.Add(new Claim("user_role", role));
        }

        var token = new JwtSecurityToken(
            issuer: null,
            audience: "authenticated",
            claims: claims,
            notBefore: DateTime.UtcNow.AddDays(-1),
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)),
            signingCredentials: new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Dispose() => _rsa.Dispose();
}
