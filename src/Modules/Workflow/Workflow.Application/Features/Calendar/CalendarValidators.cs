using Advocacia.Modules.Workflow.Domain.Calendar;
using FluentValidation;

namespace Advocacia.Modules.Workflow.Application.Features.Calendar;

public sealed class CreateCalendarEventValidator : AbstractValidator<CreateCalendarEventCommand>
{
    public CreateCalendarEventValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(CalendarEvent.TitleMaxLength);
        RuleFor(command => command.Type)
            .NotEmpty()
            .Must(type => Enum.TryParse<CalendarEventType>(type, ignoreCase: true, out _))
            .WithMessage("Tipo de evento inválido. Valores aceitos: Prazo, Audiencia, Reuniao, Tarefa.");
        RuleFor(command => command.Description).MaximumLength(4000);
        RuleFor(command => command.Location).MaximumLength(300);
    }
}

public sealed class UpdateCalendarEventValidator : AbstractValidator<UpdateCalendarEventCommand>
{
    public UpdateCalendarEventValidator()
    {
        RuleFor(command => command.EventId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(CalendarEvent.TitleMaxLength);
        RuleFor(command => command.Type)
            .NotEmpty()
            .Must(type => Enum.TryParse<CalendarEventType>(type, ignoreCase: true, out _))
            .WithMessage("Tipo de evento inválido. Valores aceitos: Prazo, Audiencia, Reuniao, Tarefa.");
        RuleFor(command => command.Description).MaximumLength(4000);
        RuleFor(command => command.Location).MaximumLength(300);
    }
}

public sealed class ListCalendarEventsValidator : AbstractValidator<ListCalendarEventsQuery>
{
    /// <summary>Teto de janela: uma consulta sem limite traria a agenda inteira do escritório.</summary>
    public const int MaxWindowDays = 62;

    public ListCalendarEventsValidator()
    {
        RuleFor(query => query.To)
            .GreaterThan(query => query.From)
            .WithMessage("O fim do período deve ser posterior ao início.");
        RuleFor(query => query)
            .Must(query => query.To - query.From <= TimeSpan.FromDays(MaxWindowDays))
            .WithMessage($"O período consultado não pode passar de {MaxWindowDays} dias.");
    }
}
