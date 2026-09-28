namespace Advocacia.BuildingBlocks.Infrastructure.Observability.HealthChecks;

/// <summary>Seção "HealthChecks" do appsettings.</summary>
public sealed class HealthChecksOptions
{
    public const string SectionName = "HealthChecks";

    public bool Enabled { get; init; } = true;

    /// <summary>Idade máxima aceitável da mensagem Pending mais antiga da outbox antes do check ficar Degraded.</summary>
    public int OutboxPendingThresholdMinutes { get; init; } = 30;

    public int DatabaseTimeoutSeconds { get; init; } = 5;

    public int RedisTimeoutSeconds { get; init; } = 3;

    public int ServiceBusTimeoutSeconds { get; init; } = 5;
}
