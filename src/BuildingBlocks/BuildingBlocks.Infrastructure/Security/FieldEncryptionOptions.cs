namespace Advocacia.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Chaves da criptografia de campos (LGPD — dado pessoal em repouso). Seção "Encryption".
/// Em produção os valores vêm do Azure Key Vault (segredos <c>Encryption--ActiveKeyId</c>,
/// <c>Encryption--Keys--{id}</c>, <c>Encryption--BlindIndexKey</c>, mapeados por
/// <c>AddAdvocaciaKeyVault</c>) — nunca de appsettings. Ver ADR-068.
/// </summary>
public sealed class FieldEncryptionOptions
{
    public const string SectionName = "Encryption";

    /// <summary>Identificador da chave usada para CIFRAR novos valores. Não pode conter ':'.</summary>
    public string ActiveKeyId { get; set; } = string.Empty;

    /// <summary>
    /// Anel de chaves: id → chave AES-256 em Base64 (32 bytes). Chaves antigas permanecem aqui
    /// para decifrar o que já foi gravado com elas; a rotação é adicionar uma chave nova,
    /// apontar <see cref="ActiveKeyId"/> para ela e re-cifrar (backfill) antes de aposentar a antiga.
    /// </summary>
    public Dictionary<string, string> Keys { get; set; } = [];

    /// <summary>
    /// Chave HMAC-SHA256 (Base64, mínimo 32 bytes) do índice cego. DISTINTA das chaves de
    /// cifragem: quem comprometer uma não ganha a outra. Trocá-la exige recalcular todos os
    /// índices (é o que o backfill faz).
    /// </summary>
    public string BlindIndexKey { get; set; } = string.Empty;

    /// <summary>
    /// Aceita ler valores sem o prefixo <c>enc:</c> (dados anteriores à Etapa 3.9) e devolvê-los
    /// como estão. Deve ser desligado assim que o backfill terminar: a partir daí, um valor em
    /// texto puro numa coluna cifrada é sinal de adulteração, não de legado.
    /// </summary>
    public bool AllowLegacyPlaintext { get; set; } = true;
}
