using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Workflow.Application.Features.Tags;

/// <summary>Idempotente: se já existe tag com o mesmo nome (sem diferenciar maiúsculas), devolve a existente.</summary>
public sealed record CreateTagCommand(string Name, string? Color = null) : ICommand<TagResponse>;

/// <summary>Apaga a tag e todas as ligações dela.</summary>
public sealed record DeleteTagCommand(Guid TagId) : ICommand;

/// <summary>
/// Coloca (<c>Applied = true</c>) ou tira a tag de um alvo. Idempotente.
/// <c>Target</c> é "Processo" ou "Advogado".
/// </summary>
public sealed record SetTagAssignmentCommand(Guid TagId, string Target, Guid TargetId, bool Applied) : ICommand;

public sealed record ListTagsQuery : IQuery<TagsResponse>;

public sealed record TagResponse(Guid Id, string Name, string Color);

public sealed record TagAssignmentResponse(Guid TagId, string Target, Guid TargetId);

public sealed record TagsResponse(IReadOnlyList<TagResponse> Items, IReadOnlyList<TagAssignmentResponse> Assignments);
