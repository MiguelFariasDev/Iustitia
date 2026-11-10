using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using FluentValidation;

namespace Advocacia.Modules.Atendimentos.Application.Features.Inbox;

public sealed class ListAtendimentosValidator : AbstractValidator<ListAtendimentosQuery>
{
    public ListAtendimentosValidator()
    {
        RuleFor(query => query.Status)
            .Must(value => Enum.TryParse<AtendimentoStatus>(value, ignoreCase: true, out _))
            .When(query => !string.IsNullOrWhiteSpace(query.Status))
            .WithMessage("Status inválido. Valores aceitos: Bot, PendingHuman, Human, Closed.");

        RuleFor(query => query.Search).MaximumLength(100);
    }
}

public sealed class SendHumanMessageValidator : AbstractValidator<SendHumanMessageCommand>
{
    public SendHumanMessageValidator()
    {
        RuleFor(command => command.AtendimentoId).NotEmpty();

        // A mensagem é dado pessoal e sigiloso: a validação nunca a repete na resposta.
        RuleFor(command => command.Body)
            .NotEmpty().WithMessage("A mensagem não pode ser vazia.")
            .MaximumLength(Atendimento.MaxBodyLength).WithMessage("A mensagem excede o limite do WhatsApp.");
    }
}

public sealed class ChangeAtendimentoStatusValidator : AbstractValidator<ChangeAtendimentoStatusCommand>
{
    private static readonly AtendimentoStatus[] Allowed =
        [AtendimentoStatus.Human, AtendimentoStatus.Bot, AtendimentoStatus.Closed];

    public ChangeAtendimentoStatusValidator()
    {
        RuleFor(command => command.AtendimentoId).NotEmpty();

        RuleFor(command => command.Status)
            .Must(value => Enum.TryParse<AtendimentoStatus>(value, ignoreCase: true, out var status)
                && Allowed.Contains(status))
            .WithMessage("Status inválido. Valores aceitos: Human (assumir), Bot (devolver), Closed (encerrar).");
    }
}

public sealed class LinkClienteValidator : AbstractValidator<LinkClienteCommand>
{
    public LinkClienteValidator()
    {
        RuleFor(command => command.AtendimentoId).NotEmpty();
        RuleFor(command => command.ClienteId).NotEmpty();
    }
}
