namespace Advocacia.Platform.Infrastructure.Auditing.Retention;

/// <summary>Usado quando AuditArchive:AccountUrl não está definido (dev/CI): o arquivamento não roda.</summary>
public sealed class NotConfiguredAuditArchiveStore : IAuditArchiveStore
{
    public bool IsConfigured => false;

    public Task<string> WriteAsync(string blobName, byte[] content, string sha256, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Arquivamento de auditoria não configurado (AuditArchive:AccountUrl).");
}
