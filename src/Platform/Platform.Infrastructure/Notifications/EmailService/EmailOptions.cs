namespace Advocacia.Platform.Infrastructure.Notifications.EmailService;

/// <summary>
/// Seção <c>Email</c>. A chave do provedor vem SEMPRE de variável de ambiente
/// (<c>Email__ApiKey</c>) ou do Key Vault — nunca do appsettings versionado.
/// Sem chave, o envio vira log (sem corpo) e a plataforma segue funcionando.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>URL base da API do provedor (Resend).</summary>
    public string ApiBaseUrl { get; set; } = "https://api.resend.com/";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Remetente verificado no provedor (SPF/DKIM/DMARC do domínio do produto).</summary>
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Iustitia";

    /// <summary>URL pública do frontend, usada nos links dos e-mails.</summary>
    public string AppBaseUrl { get; set; } = string.Empty;

    /// <summary>Tipos de alerta que também são enviados por e-mail (nome do enum AlertType).</summary>
    public string[] AlertTypes { get; set; } = ["Publication", "Task"];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(FromAddress);
}
