namespace Advocacia.Platform.Infrastructure.Auditing.Retention;

/// <summary>Armazenamento frio (Blob Storage) dos arquivos mensais de auditoria.</summary>
public interface IAuditArchiveStore
{
    /// <summary>False quando o arquivamento não está configurado: o job então não arquiva nem apaga nada.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Grava o arquivo e devolve o URI. Idempotente: se já existe um arquivo com o mesmo nome E o
    /// mesmo SHA-256 (reexecução depois de uma falha no commit), devolve o URI sem erro; se
    /// existe com conteúdo DIFERENTE, lança — nunca sobrescreve evidência.
    /// </summary>
    Task<string> WriteAsync(string blobName, byte[] content, string sha256, CancellationToken cancellationToken);
}
