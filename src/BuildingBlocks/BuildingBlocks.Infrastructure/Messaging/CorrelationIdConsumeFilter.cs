using Advocacia.BuildingBlocks.Infrastructure.Observability;
using MassTransit;

namespace Advocacia.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Propaga o CorrelationId da mensagem (já atribuído pelo MassTransit em todo publish, ver
/// ConsumeContext.CorrelationId) para o contexto ambiente de log (ver
/// <see cref="LogEnrichmentContext"/>) durante o processamento de uma mensagem — assim,
/// qualquer log emitido dentro do consumer (inclusive por serviços chamados por ele) carrega
/// o mesmo CorrelationId, sem precisar passá-lo explicitamente adiante.
/// </summary>
public sealed class CorrelationIdConsumeFilter<TMessage> : IFilter<ConsumeContext<TMessage>>
    where TMessage : class
{
    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        var previous = LogEnrichmentContext.CorrelationId;
        LogEnrichmentContext.CorrelationId = context.CorrelationId ?? Guid.NewGuid();

        try
        {
            await next.Send(context);
        }
        finally
        {
            LogEnrichmentContext.CorrelationId = previous;
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("correlationId");
}
