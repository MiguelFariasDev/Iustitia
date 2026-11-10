using Advocacia.Modules.Drafting.Application.Normas;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.Normas;

/// <summary>Object storage em disco: desenvolvimento e testes. Chaves nunca saem do diretório raiz.</summary>
public sealed class LocalObjectStorage(IOptions<NormasIngestaoOptions> options) : IObjectStorage
{
    private string Root => Path.GetFullPath(options.Value.Storage.DiretorioLocal);

    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Escrita atômica: um leitor nunca vê arquivo pela metade.
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllBytesAsync(temp, content, cancellationToken);
        File.Move(temp, path, overwrite: true);
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);

        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(Resolve(key)));

    public Task CopyAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken = default)
    {
        var from = Resolve(sourceKey);
        var to = Resolve(destinationKey);
        if (File.Exists(from))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(from, to, overwrite: true);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(Root, key.TrimStart('/')));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Chave de storage fora do diretório raiz.");
        }

        return path;
    }
}
