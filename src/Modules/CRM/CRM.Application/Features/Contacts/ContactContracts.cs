using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.CRM.Application.Features.Contacts;

/// <summary><c>Type</c> é "ParteContraria", "Testemunha", "Fornecedor" ou "Outros".</summary>
public sealed record CreateContactCommand(
    string Type,
    string Name,
    string? Document = null,
    string? Email = null,
    string? Phone = null,
    string? Notes = null) : ICommand<ContactResponse>;

public sealed record UpdateContactCommand(
    Guid ContactId,
    string Type,
    string Name,
    string? Document = null,
    string? Email = null,
    string? Phone = null,
    string? Notes = null) : ICommand;

public sealed record DeactivateContactCommand(Guid ContactId) : ICommand;

public sealed record ReactivateContactCommand(Guid ContactId) : ICommand;

public sealed record GetContactByIdQuery(Guid ContactId) : IQuery<ContactResponse>;

public sealed record ListContactsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Type = null,
    bool? OnlyActive = null,
    string? Search = null) : IQuery<ListContactsResponse>;

/// <summary>Documento e telefone saem MASCARADOS para exibição — o valor cru não sai da API.</summary>
public sealed record ContactResponse(
    Guid Id,
    string Type,
    string Name,
    string? Document,
    string? Email,
    string? Phone,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record ListContactsResponse(
    IReadOnlyList<ContactResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
