using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using FluentValidation;

namespace Advocacia.Modules.Atendimentos.Application.Features.Bot;

/// <summary>
/// Validação de FORMA. As mensagens nunca ecoam telefone nem corpo da mensagem: ambos são
/// dado pessoal e a mensagem de validação vai para o corpo da resposta e para os logs.
/// </summary>
public sealed class StartAtendimentoValidator : AbstractValidator<StartAtendimentoCommand>
{
    public StartAtendimentoValidator()
    {
        RuleFor(command => command.PhoneNumber).NotEmpty().WithMessage("O telefone é obrigatório.");
        RuleFor(command => command.ContactName).MaximumLength(Atendimento.MaxContactNameLength);
    }
}

public sealed class AddBotMessageValidator : AbstractValidator<AddBotMessageCommand>
{
    public AddBotMessageValidator()
    {
        RuleFor(command => command.AtendimentoId).NotEmpty();

        RuleFor(command => command.Direction)
            .Must(value => Enum.TryParse<MessageDirection>(value, ignoreCase: true, out _))
            .WithMessage("Direção inválida. Valores aceitos: Inbound, Outbound.");

        RuleFor(command => command.Sender)
            .Must(value => Enum.TryParse<MessageSender>(value, ignoreCase: true, out var sender)
                && sender != MessageSender.Human)
            .WithMessage("Remetente inválido para o Bot. Valores aceitos: Client, Bot.");

        RuleFor(command => command.ExternalId).MaximumLength(255);
    }
}

public sealed class RequestHandoffValidator : AbstractValidator<RequestHandoffCommand>
{
    public RequestHandoffValidator() => RuleFor(command => command.AtendimentoId).NotEmpty();
}
