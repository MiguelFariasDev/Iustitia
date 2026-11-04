using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Platform.Domain.Settings;

/// <summary>
/// Configuração arbitrária por tenant, armazenada como JSON (tabela settings).
/// AggregateRoot (não Entity simples) porque tem ciclo de vida próprio com upsert via
/// repositório genérico (Add/Update) — diferente de AuditLog, que é write-once.
/// </summary>
public sealed class Setting : AggregateRoot<SettingId>, IHasTenant
{
    public Guid TenantId { get; private set; }

    public SettingKey Key { get; private set; } = null!;

    public string ValueJson { get; private set; } = null!;

    public DateTimeOffset UpdatedAt { get; private set; }

    private Setting()
    {
        // EF Core.
    }

    private Setting(SettingId id, Guid tenantId, SettingKey key, string valueJson, DateTimeOffset updatedAt)
        : base(id)
    {
        TenantId = tenantId;
        Key = key;
        ValueJson = valueJson;
        UpdatedAt = updatedAt;
    }

    public static Result<Setting> Create(Guid tenantId, SettingKey key, string valueJson, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Setting>(ErrorFactory.From(ErrorCode.COMMON_INVALID_OPERATION));
        }

        return Result.Success(new Setting(SettingId.New(), tenantId, key, valueJson, now));
    }

    public Result UpdateValue(string valueJson, DateTimeOffset now)
    {
        ValueJson = valueJson;
        UpdatedAt = now;

        return Result.Success();
    }
}
