namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>Nomes dos ActivitySource customizados registrados no OpenTelemetry (ver OpenTelemetryConfiguration).</summary>
public static class ActivitySourceNames
{
    public const string Outbox = "Advocacia.Outbox";

    public const string Jobs = "Advocacia.Jobs";

    public const string Handlers = "Advocacia.Handlers";

    public const string Consumers = "Advocacia.MassTransit.Consumers";
}
