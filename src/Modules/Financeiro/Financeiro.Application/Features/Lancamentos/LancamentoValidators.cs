using Advocacia.Modules.Financeiro.Domain.Lancamentos;
using FluentValidation;

namespace Advocacia.Modules.Financeiro.Application.Features.Lancamentos;

public sealed class CreateLancamentoValidator : AbstractValidator<CreateLancamentoCommand>
{
    public CreateLancamentoValidator()
    {
        RuleFor(command => command.Tipo)
            .NotEmpty()
            .Must(tipo => Enum.TryParse<TipoLancamento>(tipo, ignoreCase: true, out _))
            .WithMessage("Tipo inválido. Valores aceitos: Honorario, ContaPagar, ContaReceber.");
        RuleFor(command => command.Descricao).NotEmpty().MaximumLength(Lancamento.DescricaoMaxLength);
        RuleFor(command => command.Pessoa).MaximumLength(Lancamento.PessoaMaxLength);
        RuleFor(command => command.Processo).MaximumLength(Lancamento.ProcessoMaxLength);
        RuleFor(command => command.Valor).GreaterThan(0).LessThanOrEqualTo(Lancamento.ValorMaximo);
    }
}

public sealed class ListLancamentosValidator : AbstractValidator<ListLancamentosQuery>
{
    public ListLancamentosValidator()
    {
        RuleFor(query => query.Status)
            .Must(status => status is null || Enum.TryParse<Application.Abstractions.FiltroStatusLancamento>(status, ignoreCase: true, out _))
            .WithMessage("Situação inválida. Valores aceitos: Pendente, Pago, Atrasado.");
        RuleFor(query => query.Periodo)
            .Must(periodo => periodo is null or "mes" or "proximos30")
            .WithMessage("Período inválido. Valores aceitos: mes, proximos30.");
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class GetFluxoCaixaValidator : AbstractValidator<GetFluxoCaixaQuery>
{
    public GetFluxoCaixaValidator() => RuleFor(query => query.Meses).InclusiveBetween(1, 24);
}
