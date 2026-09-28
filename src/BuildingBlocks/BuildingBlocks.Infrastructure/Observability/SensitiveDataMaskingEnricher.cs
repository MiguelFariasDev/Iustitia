using Serilog.Core;
using Serilog.Events;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Aplica <see cref="LogMaskingPolicy"/> a todo valor de propriedade string de cada log
/// event, antes de qualquer sink renderizar a mensagem — cobre tanto propriedades
/// estruturadas quanto o texto final (sinks renderizam a partir das propriedades, ver
/// ADR-037).
/// </summary>
public sealed class SensitiveDataMaskingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var (name, value) in logEvent.Properties.ToArray())
        {
            if (value is not ScalarValue { Value: string stringValue })
            {
                continue;
            }

            var masked = LogMaskingPolicy.Mask(stringValue);
            if (!ReferenceEquals(masked, stringValue) && masked != stringValue)
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(name, masked));
            }
        }
    }
}
