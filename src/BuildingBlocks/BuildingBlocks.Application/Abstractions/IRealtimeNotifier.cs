namespace Advocacia.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Envio de mensagens em tempo real ao navegador (Supabase Realtime Broadcast). Best-effort
/// por natureza: o dado já está persistido, então o frontend ainda o vê no próximo
/// refetch/polling mesmo que o push falhe.
///
/// Vive em BuildingBlocks (e não em Platform.Application) para que módulos de negócio, que
/// não podem depender de Platform.* (ADR-055), também publiquem em tempo real — ver ADR-066.
/// </summary>
public interface IRealtimeNotifier
{
    Task BroadcastAsync(string channel, string eventName, object payload, CancellationToken cancellationToken = default);
}

/// <summary>Nomes de canal/evento do Realtime — o frontend assina exatamente estes (ver web/src/features/alertas).</summary>
public static class RealtimeChannels
{
    public const string AlertCreatedEvent = "alert.created";

    /// <summary>Atendimento criado ou com status/atribuição alterados. Payload só com ids (sem dado pessoal).</summary>
    public const string AtendimentoUpdatedEvent = "atendimento.updated";

    /// <summary>Nova mensagem numa conversa. Payload só com ids — o corpo vem pela API autenticada.</summary>
    public const string AtendimentoMessageCreatedEvent = "atendimento.message.created";

    public static string Alerts(Guid tenantId, Guid userId) => $"tenant:{tenantId}:alerts:{userId}";

    /// <summary>Canal do escritório inteiro: todo advogado vê a fila de atendimentos do tenant.</summary>
    public static string Atendimentos(Guid tenantId) => $"tenant:{tenantId}:atendimentos";
}
