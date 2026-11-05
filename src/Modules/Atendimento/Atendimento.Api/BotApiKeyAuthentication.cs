using System.Security.Claims;
using System.Text.Encodings.Web;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Atendimentos.Api;

public static class BotAuthentication
{
    public const string Scheme = "BotApiKey";
    public const string Policy = "BotApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string KeyIdClaim = "bot_key_id";
}

/// <summary>
/// Autentica o Bot (server-to-server) pela chave de API no header <c>X-Api-Key</c>. A chave
/// identifica o escritório: a identidade resultante carrega só <c>tenant_id</c> — sem
/// <c>sub</c> nem papel de usuário, de modo que nenhuma policy de advogado a aceita.
/// </summary>
public sealed class BotApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IBotCredentialValidator validator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(BotAuthentication.HeaderName, out var header) || string.IsNullOrWhiteSpace(header))
        {
            return AuthenticateResult.NoResult();
        }

        var credential = await validator.ValidateAsync(header.ToString(), Context.RequestAborted);
        if (credential is null)
        {
            // Nunca loga a chave apresentada nem diz se ela existe ou foi revogada.
            Logger.LogWarning("Chave de API do Bot inválida ou revogada.");
            return AuthenticateResult.Fail("Chave de API inválida.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim("tenant_id", credential.TenantId.ToString()),
                new Claim(BotAuthentication.KeyIdClaim, credential.KeyId.ToString()),
            ],
            BotAuthentication.Scheme);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), BotAuthentication.Scheme));
    }
}
