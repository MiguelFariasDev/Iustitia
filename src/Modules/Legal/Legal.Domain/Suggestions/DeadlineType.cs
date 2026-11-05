namespace Advocacia.Modules.Legal.Domain.Suggestions;

/// <summary>
/// Forma de contagem do prazo sugerido. Persistida como texto minúsculo (business/calendar)
/// — ver <see cref="DeadlineTypeExtensions"/>.
/// </summary>
public enum DeadlineType
{
    /// <summary>Dias úteis (regra geral do CPC, art. 219).</summary>
    Business,

    /// <summary>Dias corridos.</summary>
    Calendar,
}

public static class DeadlineTypeExtensions
{
    public const string BusinessValue = "business";

    public const string CalendarValue = "calendar";

    public static string ToValue(this DeadlineType type) =>
        type == DeadlineType.Business ? BusinessValue : CalendarValue;

    public static bool TryParse(string? value, out DeadlineType type)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case BusinessValue:
                type = DeadlineType.Business;
                return true;
            case CalendarValue:
                type = DeadlineType.Calendar;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
