using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Advocacia.Platform.Infrastructure.Auditing.Retention;

/// <summary>
/// Azure Blob Storage com Managed Identity (sem chave de conta). O container deve ter política
/// de imutabilidade (WORM, retenção ≥ 5 anos) e lifecycle para a camada Cool/Archive — configurado
/// na infraestrutura (ver docs/compliance/security-hardening.md). O código nunca sobrescreve um
/// blob existente com conteúdo diferente.
/// </summary>
public sealed class AzureBlobAuditArchiveStore(IOptions<AuditArchiveOptions> options) : IAuditArchiveStore
{
    private const string Sha256Metadata = "sha256";

    private readonly Lazy<BlobContainerClient> _container = new(() =>
        new BlobServiceClient(new Uri(options.Value.AccountUrl), new DefaultAzureCredential())
            .GetBlobContainerClient(options.Value.ContainerName));

    public bool IsConfigured => options.Value.IsConfigured;

    public async Task<string> WriteAsync(string blobName, byte[] content, string sha256, CancellationToken cancellationToken)
    {
        var blob = _container.Value.GetBlobClient(blobName);

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            await blob.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    // Nunca sobrescreve: If-None-Match: * .
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                    Metadata = new Dictionary<string, string> { [Sha256Metadata] = sha256 },
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/gzip" },
                    AccessTier = AccessTier.Cool,
                },
                cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobAlreadyExists)
        {
            // Reexecução depois de uma falha entre gravar e confirmar no banco: aceita se for o mesmo conteúdo.
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (!properties.Value.Metadata.TryGetValue(Sha256Metadata, out var existing)
                || !string.Equals(existing, sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"O arquivo de auditoria '{blobName}' já existe com conteúdo diferente; recusado para não sobrescrever evidência.");
            }
        }

        return blob.Uri.ToString();
    }
}
