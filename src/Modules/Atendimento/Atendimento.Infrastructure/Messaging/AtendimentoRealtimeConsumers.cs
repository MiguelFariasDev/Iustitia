using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.Modules.Atendimentos.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Atendimentos.Infrastructure.Messaging;

// Os consumers empurram ao navegador SÓ ids e estado — nunca telefone nem texto da mensagem.
// O front, ao receber o aviso, busca o conteúdo pela API autenticada (que passa por RLS). Assim
// o canal Realtime nunca carrega dado sigiloso, mesmo que a policy do canal fosse mal configurada.
// Falha propaga: o MassTransit retenta e, esgotadas as tentativas, a UI converge no refetch.

public sealed class AtendimentoIniciadoConsumer(
    IRealtimeNotifier realtime,
    IProcessedEventStore processedEventStore,
    IAppMetrics metrics,
    ILogger<ConsumerBase<AtendimentoIniciadoEvent>> baseLogger)
    : ConsumerBase<AtendimentoIniciadoEvent>(processedEventStore, metrics, baseLogger)
{
    protected override Task HandleAsync(AtendimentoIniciadoEvent message, CancellationToken cancellationToken) =>
        realtime.BroadcastAsync(
            RealtimeChannels.Atendimentos(message.TenantId),
            RealtimeChannels.AtendimentoUpdatedEvent,
            new { atendimentoId = message.AtendimentoId },
            cancellationToken);
}

public sealed class AtendimentoStatusAlteradoConsumer(
    IRealtimeNotifier realtime,
    IProcessedEventStore processedEventStore,
    IAppMetrics metrics,
    ILogger<ConsumerBase<AtendimentoStatusAlteradoEvent>> baseLogger)
    : ConsumerBase<AtendimentoStatusAlteradoEvent>(processedEventStore, metrics, baseLogger)
{
    protected override Task HandleAsync(AtendimentoStatusAlteradoEvent message, CancellationToken cancellationToken) =>
        realtime.BroadcastAsync(
            RealtimeChannels.Atendimentos(message.TenantId),
            RealtimeChannels.AtendimentoUpdatedEvent,
            new
            {
                atendimentoId = message.AtendimentoId,
                status = message.To.ToString(),
                assignedUserId = message.AssignedUserId,
            },
            cancellationToken);
}

public sealed class AtendimentoClienteVinculadoConsumer(
    IRealtimeNotifier realtime,
    IProcessedEventStore processedEventStore,
    IAppMetrics metrics,
    ILogger<ConsumerBase<AtendimentoClienteVinculadoEvent>> baseLogger)
    : ConsumerBase<AtendimentoClienteVinculadoEvent>(processedEventStore, metrics, baseLogger)
{
    protected override Task HandleAsync(AtendimentoClienteVinculadoEvent message, CancellationToken cancellationToken) =>
        realtime.BroadcastAsync(
            RealtimeChannels.Atendimentos(message.TenantId),
            RealtimeChannels.AtendimentoUpdatedEvent,
            new { atendimentoId = message.AtendimentoId },
            cancellationToken);
}

public sealed class AtendimentoMensagemAdicionadaConsumer(
    IRealtimeNotifier realtime,
    IProcessedEventStore processedEventStore,
    IAppMetrics metrics,
    ILogger<ConsumerBase<AtendimentoMensagemAdicionadaEvent>> baseLogger)
    : ConsumerBase<AtendimentoMensagemAdicionadaEvent>(processedEventStore, metrics, baseLogger)
{
    protected override Task HandleAsync(AtendimentoMensagemAdicionadaEvent message, CancellationToken cancellationToken) =>
        realtime.BroadcastAsync(
            RealtimeChannels.Atendimentos(message.TenantId),
            RealtimeChannels.AtendimentoMessageCreatedEvent,
            new
            {
                atendimentoId = message.AtendimentoId,
                messageId = message.MensagemId,
                direction = message.Direction.ToString(),
                sender = message.Sender.ToString(),
            },
            cancellationToken);
}
