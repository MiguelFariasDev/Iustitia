using System.Net.Http.Json;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Platform.Infrastructure.Realtime;

/// <summary>
/// Publica no Supabase Realtime pela API REST de broadcast (<c>POST /realtime/v1/api/broadcast</c>),
/// autenticada com a service role — roda só no servidor, a chave nunca vai ao navegador.
///
/// Sem Supabase configurado (Url/ServiceRoleKey vazios, como em dev sem stack local) vira
/// no-op com log de debug: o push é conveniência, não requisito.
/// </summary>
public sealed class SupabaseRealtimeNotifier(
    HttpClient httpClient, IOptions<SupabaseOptions> options, ILogger<SupabaseRealtimeNotifier> logger)
    : IRealtimeNotifier
{
    public async Task BroadcastAsync(
        string channel, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.Url) || string.IsNullOrWhiteSpace(settings.ServiceRoleKey))
        {
            logger.LogDebug("Supabase Realtime não configurado — broadcast em {Channel} ignorado.", channel);
            return;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{settings.Url.TrimEnd('/')}/realtime/v1/api/broadcast")
        {
            Content = JsonContent.Create(new
            {
                messages = new[] { new { topic = channel, @event = eventName, payload, @private = true } },
            }),
        };
        request.Headers.Add("apikey", settings.ServiceRoleKey);
        request.Headers.Authorization = new("Bearer", settings.ServiceRoleKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Lança para o consumer falhar e o MassTransit retentar com backoff.
            throw new HttpRequestException(
                $"Supabase Realtime respondeu {(int)response.StatusCode} ao publicar em {channel}.");
        }
    }
}
