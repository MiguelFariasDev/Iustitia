using Advocacia.Modules.Legal.Domain.Processos;
using FluentValidation;

namespace Advocacia.Modules.Legal.Application.Features.Processos;

public sealed class CreateProcessoValidator : AbstractValidator<CreateProcessoCommand>
{
    public CreateProcessoValidator()
    {
        RuleFor(command => command.CnjNumber)
            .NotEmpty()
            .WithMessage("O número do processo é obrigatório.");

        RuleFor(command => command.ClienteId)
            .NotEmpty()
            .WithMessage("O processo precisa estar vinculado a um cliente.");

        RuleFor(command => command.Area)
            .NotEmpty()
            .Must(area => Enum.TryParse<AreaDireito>(area, ignoreCase: true, out _))
            .WithMessage("Área do direito inválida.");

        RuleFor(command => command.ValorCausa).GreaterThanOrEqualTo(0).When(command => command.ValorCausa.HasValue);
        RuleFor(command => command.Assunto).MaximumLength(300);
        RuleFor(command => command.ParteContraria).MaximumLength(200);
        RuleFor(command => command.Observacoes).MaximumLength(2000);
    }
}

public sealed class UpdateProcessoValidator : AbstractValidator<UpdateProcessoCommand>
{
    public UpdateProcessoValidator()
    {
        RuleFor(command => command.ProcessoId).NotEmpty();
        RuleFor(command => command.ClienteId).NotEmpty();
        RuleFor(command => command.ResponsavelUserId).NotEmpty();

        RuleFor(command => command.Area)
            .NotEmpty()
            .Must(area => Enum.TryParse<AreaDireito>(area, ignoreCase: true, out _))
            .WithMessage("Área do direito inválida.");

        RuleFor(command => command.ValorCausa).GreaterThanOrEqualTo(0).When(command => command.ValorCausa.HasValue);
        RuleFor(command => command.Assunto).MaximumLength(300);
        RuleFor(command => command.ParteContraria).MaximumLength(200);
        RuleFor(command => command.Observacoes).MaximumLength(2000);
    }
}

public sealed class ChangeProcessoStatusValidator : AbstractValidator<ChangeProcessoStatusCommand>
{
    public ChangeProcessoStatusValidator()
    {
        RuleFor(command => command.ProcessoId).NotEmpty();

        RuleFor(command => command.Status)
            .NotEmpty()
            .Must(status => Enum.TryParse<StatusProcesso>(status, ignoreCase: true, out _))
            .WithMessage("Situação inválida. Valores aceitos: Ativo, Suspenso, Encerrado, Arquivado.");
    }
}
