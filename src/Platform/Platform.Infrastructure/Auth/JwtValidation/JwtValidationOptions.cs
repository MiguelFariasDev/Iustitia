namespace Advocacia.Platform.Infrastructure.Auth.JwtValidation;

/// <summary>
/// Parâmetros de validação do JWT emitido pelo Supabase Auth. "Authority" é derivado de
/// SupabaseOptions.Url por padrão ({url}/auth/v1) — só sobrescrever explicitamente na
/// seção "Jwt" do appsettings se o projeto usar um domínio customizado.
/// </summary>
public sealed class JwtValidationOptions
{
    public const string SectionName = "Jwt";

    public string Authority { get; set; } = string.Empty;

    /// <summary>Claim "aud" emitida pelo Supabase Auth para usuários autenticados — não confundir com API keys.</summary>
    public string Audience { get; set; } = "authenticated";

    public bool RequireHttpsMetadata { get; set; } = true;

    public bool ValidateIssuer { get; set; } = true;

    public bool ValidateAudience { get; set; } = true;

    public bool ValidateLifetime { get; set; } = true;

    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);
}
