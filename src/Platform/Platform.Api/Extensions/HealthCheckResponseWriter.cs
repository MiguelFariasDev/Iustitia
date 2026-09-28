using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Advocacia.Platform.Api.Extensions;

/// <summary>Formato JSON estruturado para o endpoint /health (todos os checks, com detalhes) — ver docs/observability/health-checks.md.</summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = entry.Value.Duration.TotalMilliseconds,
                tags = entry.Value.Tags,
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
    }
}
