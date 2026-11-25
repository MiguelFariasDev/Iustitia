using Advocacia.Modules.Legal.Application.Abstractions;
using FluentValidation;

namespace Advocacia.Modules.Legal.Application.Features.Movimentacoes;

public sealed class SincronizarMovimentacoesValidator : AbstractValidator<SincronizarMovimentacoesCommand>
{
    public SincronizarMovimentacoesValidator()
    {
        RuleFor(command => command.ProcessoId).NotEmpty();
        RuleFor(command => command.TrocarParaFonte)
            .Must(fonte => fonte is null or FonteConsulta.Juscraper or FonteConsulta.DataJud)
            .WithMessage("Fonte inválida. Valores aceitos: juscraper, datajud.");
        RuleFor(command => command.JanelaDias).InclusiveBetween(0, 30);
    }
}

public sealed class ListMovimentacoesValidator : AbstractValidator<ListMovimentacoesQuery>
{
    public ListMovimentacoesValidator()
    {
        RuleFor(query => query.ProcessoId).NotEmpty();
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
