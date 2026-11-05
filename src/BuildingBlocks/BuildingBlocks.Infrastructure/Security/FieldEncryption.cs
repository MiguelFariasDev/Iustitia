namespace Advocacia.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Ponto de acesso estático ao <see cref="IFieldEncryptor"/> para os ValueConverters do EF Core.
///
/// Existe porque o EF Core compila o modelo UMA vez por tipo de DbContext e o cacheia: os
/// conversores são criados em OnModelCreating, sem acesso ao escopo de DI da requisição. O
/// conversor guarda só a chamada a este acessor, resolvida na hora de converter. É configurado
/// no startup (<c>AddAdvocaciaFieldEncryption</c>) e, se não estiver, FALHA em vez de gravar em
/// texto puro: um campo que deveria ser cifrado nunca é gravado sem cifra por esquecimento.
/// </summary>
public static class FieldEncryption
{
    private static IFieldEncryptor? _current;

    public static IFieldEncryptor Current =>
        Volatile.Read(ref _current)
        ?? throw new InvalidOperationException(
            "Criptografia de campos não configurada. Chame AddAdvocaciaFieldEncryption no startup (ou FieldEncryption.Configure em testes).");

    public static void Configure(IFieldEncryptor encryptor) => Volatile.Write(ref _current, encryptor);

    public static string Encrypt(string plaintext, string context) => Current.Encrypt(plaintext, context);

    public static string Decrypt(string stored, string context) => Current.Decrypt(stored, context);

    public static string BlindIndex(string value, string context) => Current.BlindIndex(value, context);
}
