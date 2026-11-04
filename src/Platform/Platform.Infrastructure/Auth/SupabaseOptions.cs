namespace Advocacia.Platform.Infrastructure.Auth;

/// <summary>
/// Configuração do Supabase, ligada à seção "Supabase" do appsettings. Em produção, Url/
/// AnonKey/ServiceRoleKey vêm do Azure Key Vault — nunca commitar valores reais aqui ou em
/// appsettings.json (ver docs/compliance/security.md).
/// </summary>
public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    /// <summary>Ex.: https://xxxxx.supabase.co</summary>
    public string Url { get; set; } = string.Empty;

    public string AnonKey { get; set; } = string.Empty;

    /// <summary>Só usada server-side para operações admin (invite, get user por id) — nunca exposta ao cliente.</summary>
    public string ServiceRoleKey { get; set; } = string.Empty;
}
