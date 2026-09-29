namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Nomes dos ActivitySource customizados registrados no OpenTelemetry (ver
/// OpenTelemetryConfiguration). Módulos de negócio declaram aqui o nome do seu
/// ActivitySource — só a constante, para o registro central poder incluí-lo: o
/// ActivitySource em si é criado no módulo (ver CnjCaptureTelemetry) e BuildingBlocks
/// continua sem nenhuma dependência de Advocacia.Modules.*.
/// </summary>
public static class ActivitySourceNames
{
    public const string Outbox = "Advocacia.Outbox";

    public const string Jobs = "Advocacia.Jobs";

    public const string Handlers = "Advocacia.Handlers";

    public const string Consumers = "Advocacia.MassTransit.Consumers";

    /// <summary>Captura de publicações do DJEN/CNJ (módulo Legal — ver CnjCaptureTelemetry).</summary>
    public const string CnjCapture = "Advocacia.CNJ.Capture";
}
