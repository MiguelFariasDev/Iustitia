using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Legal.Application.Features.Processos;

/// <summary>
/// Uma movimentação do processo, derivada de uma PUBLICAÇÃO capturada do diário (DJEN).
/// </summary>
/// <param name="Id">Id da publicação que originou a movimentação.</param>
/// <param name="Data">Data da divulgação no diário (AAAA-MM-DD).</param>
/// <param name="Tipo">Natureza do ato (<c>Decisao</c>, <c>Despacho</c>, <c>Intimacao</c>, <c>Sentenca</c>, <c>Acordao</c>, <c>Outros</c>); <c>Publicacao</c> quando o tipo não foi classificado.</param>
/// <param name="Descricao">Trecho do texto (até 280 caracteres). NUNCA o inteiro teor: é dado sob sigilo profissional.</param>
/// <param name="Fonte">Origem da movimentação (hoje, o diário: <c>Djen</c>).</param>
/// <param name="Lida">Se alguém do escritório já abriu a publicação.</param>
public sealed record MovimentacaoResponse(Guid Id, DateOnly Data, string Tipo, string Descricao, string Fonte, bool Lida);

public sealed record MovimentacoesPageResponse(
    IReadOnlyList<MovimentacaoResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages);

/// <summary>Movimentações do processo, da mais recente para a mais antiga.</summary>
public sealed record ListMovimentacoesQuery(Guid ProcessoId, int Page = 1, int PageSize = 50)
    : IQuery<MovimentacoesPageResponse>;
