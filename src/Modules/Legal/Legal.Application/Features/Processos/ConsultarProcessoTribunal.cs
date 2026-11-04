using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Shared;
using FluentValidation;

namespace Advocacia.Modules.Legal.Application.Features.Processos;

/// <summary>
/// Consulta os metadados do processo no tribunal para auto-preencher o cadastro.
/// O tribunal NÃO é parâmetro: ele está codificado no próprio número CNJ (campo J.TR), e
/// pedi-lo ao usuário abriria espaço para divergir do número — um processo do TJSP
/// consultado no índice do TJRJ simplesmente não seria encontrado, sem erro nenhum.
/// </summary>
public sealed record ConsultarProcessoTribunalQuery(string CnjNumber) : IQuery<ConsultarProcessoTribunalResponse>;

/// <param name="Found">Falso quando o tribunal respondeu, mas não conhece o processo.</param>
/// <param name="Tribunal">Sigla derivada do número CNJ, útil na tela mesmo sem resultado.</param>
/// <param name="Processo">Metadados encontrados, ou nulo.</param>
public sealed record ConsultarProcessoTribunalResponse(
    bool Found,
    string? Tribunal,
    ProcessoTribunal? Processo);

public sealed class ConsultarProcessoTribunalValidator : AbstractValidator<ConsultarProcessoTribunalQuery>
{
    public ConsultarProcessoTribunalValidator()
    {
        RuleFor(query => query.CnjNumber)
            .NotEmpty()
            .WithErrorCode(nameof(ErrorCode.PROCESS_CNJ_INVALID))
            .Must(numero => CNJNumber.Create(numero).IsSuccess)
            .WithErrorCode(nameof(ErrorCode.PROCESS_CNJ_INVALID))
            .WithMessage("Número CNJ inválido.");
    }
}

public sealed class ConsultarProcessoTribunalHandler(IConsultaProcessoTribunal consulta)
    : IQueryHandler<ConsultarProcessoTribunalQuery, ConsultarProcessoTribunalResponse>
{
    public async Task<Result<ConsultarProcessoTribunalResponse>> Handle(
        ConsultarProcessoTribunalQuery request, CancellationToken cancellationToken)
    {
        var cnjNumber = CNJNumber.Create(request.CnjNumber);
        if (cnjNumber.IsFailure)
        {
            return Result.Failure<ConsultarProcessoTribunalResponse>(ErrorFactory.From(ErrorCode.PROCESS_CNJ_INVALID));
        }

        var tribunal = cnjNumber.Value.Tribunal;

        var resultado = await consulta.ConsultarAsync(cnjNumber.Value, cancellationToken);
        if (resultado.IsFailure)
        {
            return Result.Failure<ConsultarProcessoTribunalResponse>(resultado.Error);
        }

        // Não encontrado devolve SUCESSO com Found=false, não erro: o cadastro manual segue
        // disponível e a tela precisa distinguir "não existe lá" de "a consulta falhou".
        return resultado.Value is { } processo
            ? new ConsultarProcessoTribunalResponse(Found: true, tribunal, processo)
            : new ConsultarProcessoTribunalResponse(Found: false, tribunal, Processo: null);
    }
}
