namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>Configuração do OutboxProcessor/OutboxCleanupService — seção "Outbox" do appsettings.</summary>
public sealed class OutboxProcessorOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; init; } = 100;

    public int ProcessingIntervalSeconds { get; init; } = 10;

    public int MaxRetries { get; init; } = 5;

    public int RetentionDays { get; init; } = 30;

    public int BackoffBaseSeconds { get; init; } = 2;
}
