using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Infrastructure.Observability;
using Microsoft.Extensions.Options;

namespace Advocacia.Platform.Api.Middleware;

/// <summary>Seção <c>Security:Mfa</c>. Desligado por padrão: ligar sem antes cadastrar o TOTP dos sócios os trancaria fora.</summary>
public sealed class MfaOptions
{
    public const string SectionName = "Security:Mfa";

    /// <summary>Liga a exigência de MFA para os papéis em <see cref="RequiredRoles"/>.</summary>
    public bool Enforce { get; set; }

    /// <summary>Papéis (claim <c>user_role</c>) obrigados a ter completado o MFA nesta sessão.</summary>
    public string[] RequiredRoles { get; set; } = ["Owner", "Partner"];
}

/// <summary>
/// Exige segundo fator dos papéis privilegiados. O Supabase Auth marca no JWT o nível de garantia da
/// sessão: <c>aal1</c> (só senha) ou <c>aal2</c> (senha + TOTP verificado). Quem tem papel obrigado e
/// <c>aal</c> diferente de <c>aal2</c> recebe 401 <c>AUTH_MFA_REQUIRED</c> — o frontend então leva ao
/// cadastro/desafio do TOTP. Ficam fora da exigência só as rotas de que o usuário precisa para
/// descobrir isso e completar o desafio (perfil e autenticação) e a saúde do serviço.
/// </summary>
public sealed class MfaEnforcementMiddleware(RequestDelegate next, IOptions<MfaOptions> options)
{
    private static readonly string[] ExemptPrefixes =
        ["/api/v1/auth", "/api/v1/me", "/health", "/api/v1/health"];

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        var settings = options.Value;

        if (settings.Enforce && currentUser.IsAuthenticated && RequiresMfa(settings, currentUser)
            && !HasCompletedMfa(context) && !IsExempt(context.Request.Path))
        {
            var error = ErrorFactory.From(ErrorCode.AUTH_MFA_REQUIRED);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Results.Problem(
                title: error.Code,
                detail: error.Message,
                statusCode: StatusCodes.Status401Unauthorized,
                extensions: new Dictionary<string, object?>
                {
                    ["errorType"] = error.Type.ToString(),
                    ["errorCode"] = error.Code,
                    ["correlationId"] = LogEnrichmentContext.CorrelationId,
                    ["timestamp"] = DateTimeOffset.UtcNow,
                }).ExecuteAsync(context);

            return;
        }

        await next(context);
    }

    internal static bool RequiresMfa(MfaOptions settings, ICurrentUser user) =>
        settings.RequiredRoles.Any(role => user.IsInRole(role));

    internal static bool HasCompletedMfa(HttpContext context) =>
        string.Equals(context.User.FindFirst("aal")?.Value, "aal2", StringComparison.Ordinal);

    internal static bool IsExempt(PathString path) =>
        ExemptPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}
