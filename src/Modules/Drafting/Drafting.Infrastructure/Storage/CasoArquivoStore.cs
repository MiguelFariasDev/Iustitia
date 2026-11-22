using System.Security.Cryptography;
using System.Text;
using Advocacia.BuildingBlocks.Infrastructure.Security;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Normas;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.Storage;

/// <summary>
/// Arquivos de caso cifrados com AES-256-GCM ANTES de sair do processo: o provedor de storage (e quem ler o
/// bucket) só vê bytes cifrados. Usa o mesmo anel de chaves da criptografia de campos (seção
/// <c>Encryption</c>), com rotação pelo id da chave no cabeçalho. A chave do objeto é dado autenticado: um
/// arquivo copiado para o lugar de outro não decifra.
/// Formato: "IACF" | versão (1) | tamanho do id (1) | id da chave | nonce (12) | tag (16) | conteúdo cifrado.
/// </summary>
public sealed class CasoArquivoStore(IObjectStorage storage, IOptions<FieldEncryptionOptions> chaves) : ICasoArquivoStore
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const byte Versao = 1;

    private static readonly byte[] Magia = "IACF"u8.ToArray();

    public Task GravarAsync(string chave, byte[] conteudo, CancellationToken cancellationToken = default) =>
        ExecutarAsync(async () =>
        {
            await storage.PutAsync(chave, Cifrar(chave, conteudo), "application/octet-stream", cancellationToken);

            return true;
        });

    public Task<byte[]?> LerAsync(string chave, CancellationToken cancellationToken = default) =>
        ExecutarAsync(async () =>
        {
            var cifrado = await storage.GetAsync(chave, cancellationToken);

            return cifrado is null ? null : Decifrar(chave, cifrado);
        });

    public Task RemoverAsync(string chave, CancellationToken cancellationToken = default) =>
        ExecutarAsync(async () =>
        {
            await storage.DeleteAsync(chave, cancellationToken);

            return true;
        });

    private byte[] Cifrar(string chaveObjeto, byte[] conteudo)
    {
        var id = chaves.Value.ActiveKeyId;
        var idBytes = Encoding.UTF8.GetBytes(id);
        var cabecalho = Magia.Length + 2 + idBytes.Length;
        var saida = new byte[cabecalho + NonceSize + TagSize + conteudo.Length];

        Magia.CopyTo(saida, 0);
        saida[Magia.Length] = Versao;
        saida[Magia.Length + 1] = checked((byte)idBytes.Length);
        idBytes.CopyTo(saida, Magia.Length + 2);

        var nonce = saida.AsSpan(cabecalho, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(Chave(id), TagSize);
        aes.Encrypt(
            nonce, conteudo, saida.AsSpan(cabecalho + NonceSize + TagSize), saida.AsSpan(cabecalho + NonceSize, TagSize),
            Encoding.UTF8.GetBytes(chaveObjeto));

        return saida;
    }

    private byte[] Decifrar(string chaveObjeto, byte[] dados)
    {
        if (dados.Length < Magia.Length + 2 || !dados.AsSpan(0, Magia.Length).SequenceEqual(Magia) || dados[Magia.Length] != Versao)
        {
            throw new CryptographicException("Arquivo de caso sem o cabeçalho de criptografia.");
        }

        var tamanhoId = dados[Magia.Length + 1];
        var cabecalho = Magia.Length + 2 + tamanhoId;
        if (dados.Length < cabecalho + NonceSize + TagSize)
        {
            throw new CryptographicException("Arquivo de caso truncado.");
        }

        var id = Encoding.UTF8.GetString(dados, Magia.Length + 2, tamanhoId);
        var saida = new byte[dados.Length - cabecalho - NonceSize - TagSize];
        using var aes = new AesGcm(Chave(id), TagSize);
        aes.Decrypt(
            dados.AsSpan(cabecalho, NonceSize), dados.AsSpan(cabecalho + NonceSize + TagSize), dados.AsSpan(cabecalho + NonceSize, TagSize),
            saida, Encoding.UTF8.GetBytes(chaveObjeto));

        return saida;
    }

    private byte[] Chave(string id) =>
        chaves.Value.Keys.TryGetValue(id, out var base64)
            ? Convert.FromBase64String(base64)
            : throw new CryptographicException($"Chave de criptografia '{id}' não está no anel de chaves.");

    /// <summary>Rede e disco viram <see cref="StorageException"/> (transitória). Falha de integridade NÃO: propaga.</summary>
    private static async Task<T> ExecutarAsync<T>(Func<Task<T>> acao)
    {
        try
        {
            return await acao();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or TaskCanceledException { InnerException: TimeoutException })
        {
            throw new StorageException("O armazenamento de documentos não respondeu.", ex);
        }
    }
}
