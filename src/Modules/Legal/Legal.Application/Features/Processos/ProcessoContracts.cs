using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Legal.Application.Features.Processos;

public sealed record ForoRequest(string? Tribunal, string? Comarca, string? Vara);

/// <summary>
/// Cadastro de processo. <c>CnjNumber</c> aceita o número com ou sem máscara;
/// <c>ResponsavelUserId</c> nulo assume o usuário que está cadastrando.
/// </summary>
public sealed record CreateProcessoCommand(
    string CnjNumber,
    Guid ClienteId,
    string Area,
    Guid? ResponsavelUserId = null,
    ForoRequest? Foro = null,
    string? Assunto = null,
    string? ParteContraria = null,
    decimal? ValorCausa = null,
    DateOnly? DataDistribuicao = null,
    string? Observacoes = null) : ICommand<ProcessoResponse>;

/// <summary>O número CNJ NÃO entra: é a identidade do processo e a chave das publicações já vinculadas.</summary>
public sealed record UpdateProcessoCommand(
    Guid ProcessoId,
    Guid ClienteId,
    Guid ResponsavelUserId,
    string Area,
    ForoRequest? Foro = null,
    string? Assunto = null,
    string? ParteContraria = null,
    decimal? ValorCausa = null,
    DateOnly? DataDistribuicao = null,
    string? Observacoes = null) : ICommand;

public sealed record ChangeProcessoStatusCommand(Guid ProcessoId, string Status) : ICommand;

public sealed record ArchiveProcessoCommand(Guid ProcessoId) : ICommand;

public sealed record GetProcessoByIdQuery(Guid ProcessoId) : IQuery<ProcessoResponse>;

public sealed record ListProcessosQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? ClienteId = null,
    Guid? ResponsavelUserId = null,
    string? Area = null,
    string? Status = null,
    string? Busca = null,
    string? Tribunal = null,
    DateOnly? DistribuicaoDe = null,
    DateOnly? DistribuicaoAte = null) : IQuery<ListProcessosResponse>;

public sealed record ProcessoResponse(
    Guid Id,
    string CnjNumber,
    Guid ClienteId,
    Guid ResponsavelUserId,
    string Area,
    string Status,
    string? Tribunal,
    string? Comarca,
    string? Vara,
    string? Assunto,
    string? ParteContraria,
    decimal? ValorCausa,
    DateOnly? DataDistribuicao,
    string? Observacoes,
    bool GeraCaptura,
    DateTimeOffset CreatedAt);

public sealed record ProcessoListItem(
    Guid Id,
    string CnjNumber,
    Guid ClienteId,
    string Area,
    string Status,
    string? Tribunal,
    string? ParteContraria,
    DateOnly? DataDistribuicao);

public sealed record ListProcessosResponse(
    IReadOnlyList<ProcessoListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
