using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Advocacia.Platform.Infrastructure.Auditing.Retention;

/// <summary>
/// NDJSON (um evento por linha) comprimido com gzip. Determinístico: as linhas já vêm ordenadas
/// (created_at, id) do banco e o cabeçalho gzip do .NET não carrega data — o mesmo mês gera os
/// mesmos bytes, o que torna a gravação idempotente (comparação por SHA-256).
/// </summary>
public static class AuditArchiveFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static (byte[] Content, string Sha256) Serialize(IEnumerable<AuditLogArchiveRow> rows)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            foreach (var row in rows)
            {
                gzip.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row, JsonOptions)));
                gzip.WriteByte((byte)'\n');
            }
        }

        var content = buffer.ToArray();
        return (content, Convert.ToHexStringLower(SHA256.HashData(content)));
    }

    public static string BlobName(Guid tenantId, DateOnly month) =>
        $"{tenantId:D}/{month.Year:0000}/{month:yyyy-MM}.ndjson.gz";
}
