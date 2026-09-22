namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;

public enum OutboxStatus
{
    Pending,
    Processed,
    Failed,
}
