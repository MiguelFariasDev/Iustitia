namespace Advocacia.Modules.Atendimentos.Application.Abstractions;

/// <summary>Identidade resolvida a partir de uma chave de API do Bot: qual chave e de qual escritório.</summary>
public sealed record BotCredential(Guid KeyId, Guid TenantId);

/// <summary>
/// Valida a chave de API apresentada pelo Bot. Devolve <c>null</c> para chave desconhecida ou
/// revogada — sem distinguir os dois casos, para não ajudar quem estiver testando chaves.
/// </summary>
public interface IBotCredentialValidator
{
    Task<BotCredential?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default);
}
