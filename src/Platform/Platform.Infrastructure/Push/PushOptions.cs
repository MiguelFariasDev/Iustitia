namespace Advocacia.Platform.Infrastructure.Push;

/// <summary>
/// Configuração do envio de push (<c>Push:Fcm</c>). Sem credenciais o push fica DESLIGADO
/// (<see cref="NoopPushNotificationService"/>) e o resto do sistema segue normal — as credenciais
/// de uma conta de serviço do Firebase nunca vão para o repositório.
/// </summary>
public sealed class PushOptions
{
    public const string SectionName = "Push:Fcm";

    /// <summary>Caminho de um arquivo JSON da conta de serviço do Firebase (ex.: montado como secret no contêiner).</summary>
    public string? CredentialsPath { get; init; }

    /// <summary>O conteúdo do JSON da conta de serviço (ex.: vindo do Key Vault). Tem precedência sobre o caminho.</summary>
    public string? CredentialsJson { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CredentialsJson) || !string.IsNullOrWhiteSpace(CredentialsPath);
}
