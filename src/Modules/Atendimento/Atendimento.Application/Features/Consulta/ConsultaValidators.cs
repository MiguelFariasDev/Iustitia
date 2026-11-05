using FluentValidation;

namespace Advocacia.Modules.Atendimentos.Application.Features.Consulta;

public sealed class IniciarConsultaValidator : AbstractValidator<IniciarConsultaCommand>
{
    public IniciarConsultaValidator() => RuleFor(command => command.AtendimentoId).NotEmpty();
}

/// <summary>Validação de FORMA. A mensagem nunca ecoa o documento: ela vai para o corpo da resposta e para os logs.</summary>
public sealed class VerificarConsultaValidator : AbstractValidator<VerificarConsultaCommand>
{
    public VerificarConsultaValidator()
    {
        RuleFor(command => command.AtendimentoId).NotEmpty();

        RuleFor(command => command.Documento)
            .NotEmpty().WithMessage("O documento é obrigatório.")
            .MaximumLength(30).WithMessage("O documento informado é longo demais.");
    }
}

public sealed class GetConsultaProcessoValidator : AbstractValidator<GetConsultaProcessoQuery>
{
    public GetConsultaProcessoValidator()
    {
        RuleFor(query => query.AtendimentoId).NotEmpty();
        RuleFor(query => query.Indice).GreaterThanOrEqualTo(1).WithMessage("O índice do processo começa em 1.");
    }
}
