using Advocacia.Modules.Legal.Domain.Movimentacoes;
using FluentValidation;

namespace Advocacia.Modules.Legal.Application.Features.NotificacoesCliente;

public sealed class ListNotificacoesClienteValidator : AbstractValidator<ListNotificacoesClienteQuery>
{
    public ListNotificacoesClienteValidator()
    {
        RuleFor(query => query.Status)
            .Must(status => status is null || Enum.TryParse<StatusNotificacaoCliente>(status, ignoreCase: true, out _))
            .WithMessage("Situação inválida. Valores aceitos: PendenteRevisao, Enviada, Descartada, FalhaEnvio.");
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class EnviarNotificacaoClienteValidator : AbstractValidator<EnviarNotificacaoClienteCommand>
{
    public EnviarNotificacaoClienteValidator()
    {
        RuleFor(command => command.NotificacaoId).NotEmpty();
        RuleFor(command => command.Mensagem)
            .Must(mensagem => mensagem is null || (!string.IsNullOrWhiteSpace(mensagem) && mensagem.Trim().Length <= NotificacaoCliente.MensagemMaxLength))
            .WithErrorCode("PROCESS_CLIENT_NOTIFICATION_MESSAGE_INVALID")
            .WithMessage("A mensagem do aviso não pode ficar vazia nem passar de 8.000 caracteres.");
    }
}

public sealed class DescartarNotificacaoClienteValidator : AbstractValidator<DescartarNotificacaoClienteCommand>
{
    public DescartarNotificacaoClienteValidator() => RuleFor(command => command.NotificacaoId).NotEmpty();
}
