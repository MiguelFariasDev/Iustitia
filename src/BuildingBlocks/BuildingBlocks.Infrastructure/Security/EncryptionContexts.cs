namespace Advocacia.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Contextos de criptografia, um por coluna. São parte do dado autenticado (AAD) e do HMAC:
/// MUDAR uma destas strings invalida tudo que já foi gravado — por isso são constantes
/// independentes do nome físico da coluna (que pode ser renomeada sem afetar a criptografia).
/// </summary>
public static class EncryptionContexts
{
    public const string ClienteDocumento = "cliente.documento";
    public const string ClienteDocumentoBlindIndex = "cliente.documento.blind";
    public const string ClienteEmail = "cliente.email";
    public const string ClienteTelefone = "cliente.telefone";
    public const string UserOabNumber = "user.oab_number";
    public const string UserPhone = "user.phone";
}
