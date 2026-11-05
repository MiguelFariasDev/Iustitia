using Advocacia.BuildingBlocks.Domain.Time;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Workflow.Application;

/// <summary>
/// Decide "que dia é hoje" no fuso do escritório. É a peça que impede "vence hoje" de errar o dia à
/// noite: às 22h em São Paulo já é o dia seguinte em UTC, e um prazo "de hoje" não pode virar "de
/// amanhã" nem "atrasado" por causa do relógio do servidor.
/// </summary>
public sealed class BusinessCalendar
{
    private readonly IDateTimeProvider _clock;
    private readonly TimeZoneInfo _zone;

    public BusinessCalendar(IDateTimeProvider clock, IOptions<WorkflowOptions> options)
    {
        _clock = clock;
        // Falha na criação, não em runtime numa consulta: fuso inexistente é erro de configuração.
        _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
    }

    public DateOnly Today => LocalDate(_clock.UtcNow);

    public DateOnly LocalDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, _zone).DateTime);

    /// <summary>O instante (UTC) em que o dia local começa.</summary>
    public DateTimeOffset StartOfDayUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, _zone), TimeSpan.Zero);
    }
}
