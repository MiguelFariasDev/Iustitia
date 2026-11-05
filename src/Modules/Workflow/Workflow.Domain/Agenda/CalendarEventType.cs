namespace Advocacia.Modules.Workflow.Domain.Agenda;

/// <summary>Prazos NÃO são um tipo de evento: são <c>WorkTask</c>, e a agenda os mostra como "Deadline".</summary>
public enum CalendarEventType
{
    Hearing,
    Meeting,
    Other,
}
