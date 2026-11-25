using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Domain.Shared;

namespace Advocacia.Modules.Legal.Application.Abstractions;

/// <summary>
/// Porta do ACOMPANHAMENTO de movimentações (ADR-075): busca os andamentos de um processo no
/// tribunal para o job noturno e a sincronização sob demanda. Diferente da consulta do cadastro
/// (<see cref="IConsultaProcessoTribunal"/>), aqui o fallback é restrito: misturar fontes no mesmo
/// processo duplicaria andamentos (nomes e ids diferentes para o mesmo evento), então o processo
/// fica na fonte em que começou.
/// </summary>
public interface IAcompanhamentoProcessual
{
    /// <param name="fonte">
    /// Fonte em que o processo já é acompanhado ("juscraper"/"datajud"), ou nula na primeira
    /// sincronização — quando a implementação escolhe (juscraper; DataJud se o tribunal não tiver
    /// cobertura permanente no juscraper).
    /// </param>
    /// <param name="desde">Só andamentos nesta data ou depois; nulo traz o histórico inteiro.</param>
    /// <returns>
    /// Falha com CNJ_TRIBUNAL_NOT_SUPPORTED quando nenhuma fonte permitida cobre o tribunal; outras
    /// falhas são transitórias (a próxima noite tenta de novo).
    /// </returns>
    Task<Result<AndamentosCapturados>> ObterAsync(
        CNJNumber cnjNumber, string? fonte, DateOnly? desde, CancellationToken cancellationToken = default);
}

/// <param name="Encontrado">Falso: a fonte respondeu e não conhece o processo.</param>
/// <param name="Fonte">"juscraper" ou "datajud" — a que efetivamente respondeu.</param>
/// <param name="Completas">Falso quando a fonte truncou a lista (teto de páginas).</param>
/// <param name="Andamentos">Do mais recente para o mais antigo.</param>
public sealed record AndamentosCapturados(
    bool Encontrado,
    string Fonte,
    bool Completas,
    IReadOnlyList<AndamentoCapturado> Andamentos);

/// <param name="ExternalId">Id estável na fonte; andamento sem id é ignorado.</param>
public sealed record AndamentoCapturado(
    string? ExternalId,
    string Nome,
    string? Complemento,
    DateTimeOffset? DataHora,
    string? Instancia,
    bool PossuiDocumento);
