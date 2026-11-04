using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.BuildingBlocks.Infrastructure.Messaging;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Advocacia.Platform.Domain.FeatureFlags;
using Advocacia.Platform.Domain.Settings;
using Advocacia.Platform.Domain.Tenancy.Events;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Infrastructure.Messaging.Consumers;

/// <summary>Recebe TenantCreatedEvent (via outbox) e cria settings e feature flags padrão do tenant.</summary>
public sealed class TenantCreatedConsumer(
    IRepository<Setting, SettingId> settingRepository,
    IRepository<FeatureFlag, FeatureFlagId> featureFlagRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IProcessedEventStore processedEventStore,
    IAppMetrics metrics,
    ILogger<ConsumerBase<TenantCreatedEvent>> logger)
    : ConsumerBase<TenantCreatedEvent>(processedEventStore, metrics, logger)
{
    protected override async Task HandleAsync(TenantCreatedEvent message, CancellationToken cancellationToken)
    {
        var tenantId = message.TenantId.Value;
        var now = dateTimeProvider.UtcNow;

        var localeKey = SettingKey.Create("locale");
        if (localeKey.IsSuccess)
        {
            var defaultSetting = Setting.Create(tenantId, localeKey.Value, "\"pt-BR\"", now);
            if (defaultSetting.IsSuccess)
            {
                settingRepository.Add(defaultSetting.Value);
            }
        }

        var athenaKey = FeatureFlagKey.Create("athena.assistant");
        if (athenaKey.IsSuccess)
        {
            var defaultFeatureFlag = FeatureFlag.Create(tenantId, athenaKey.Value, isEnabled: false, now);
            if (defaultFeatureFlag.IsSuccess)
            {
                featureFlagRepository.Add(defaultFeatureFlag.Value);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
