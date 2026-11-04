namespace Advocacia.Platform.Application.Abstractions;

/// <summary>
/// Envio de mensagens em tempo real ao navegador (Supabase Realtime Broadcast). Best-effort
/// por natureza: o alerta já está persistido, então o frontend ainda o vê no próximo
/// refetch/polling mesmo que o push falhe.
/// </summary>
public interface IRealtimeNotifier
{
    Task BroadcastAsync(string channel, string eventName, object payload, CancellationToken cancellationToken = default);
}

/// <summary>Nomes de canal/evento do Realtime — o frontend assina exatamente estes (ver web/src/features/alertas).</summary>
public static class RealtimeChannels
{
    public const string AlertCreatedEvent = "alert.created";

    public static string Alerts(Guid tenantId, Guid userId) => $"tenant:{tenantId}:alerts:{userId}";
}
