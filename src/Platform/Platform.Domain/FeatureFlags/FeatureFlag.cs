using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Platform.Domain.FeatureFlags;

/// <summary>
/// Feature flag por tenant, controlada via Microsoft.FeatureManagement. AggregateRoot
/// (não Entity simples) porque tem ciclo de vida próprio com upsert via repositório
/// genérico (Add/Update) — diferente de AuditLog, que é write-once.
/// </summary>
public sealed class FeatureFlag : AggregateRoot<FeatureFlagId>, IHasTenant
{
    public Guid TenantId { get; private set; }

    public FeatureFlagKey Key { get; private set; } = null!;

    public bool IsEnabled { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private FeatureFlag()
    {
        // EF Core.
    }

    private FeatureFlag(FeatureFlagId id, Guid tenantId, FeatureFlagKey key, bool isEnabled, DateTimeOffset updatedAt)
        : base(id)
    {
        TenantId = tenantId;
        Key = key;
        IsEnabled = isEnabled;
        UpdatedAt = updatedAt;
    }

    public static Result<FeatureFlag> Create(Guid tenantId, FeatureFlagKey key, bool isEnabled, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<FeatureFlag>(ErrorFactory.From(ErrorCode.COMMON_INVALID_OPERATION));
        }

        return Result.Success(new FeatureFlag(FeatureFlagId.New(), tenantId, key, isEnabled, now));
    }

    public Result Enable(DateTimeOffset now)
    {
        if (IsEnabled)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.COMMON_INVALID_OPERATION));
        }

        IsEnabled = true;
        UpdatedAt = now;

        return Result.Success();
    }

    public Result Disable(DateTimeOffset now)
    {
        if (!IsEnabled)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.COMMON_INVALID_OPERATION));
        }

        IsEnabled = false;
        UpdatedAt = now;

        return Result.Success();
    }
}
