using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.CRM.Application.Abstractions;
using Advocacia.Modules.CRM.Domain.Contacts;

namespace Advocacia.Modules.CRM.Application.Features.Contacts;

/// <summary>O tenant vem SEMPRE do JWT (ICurrentUser), nunca do corpo da requisição.</summary>
public sealed class CreateContactHandler(
    IContactRepository contactRepository,
    ICrmUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<CreateContactCommand, ContactResponse>
{
    public async Task<Result<ContactResponse>> Handle(CreateContactCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<ContactResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        if (!Enum.TryParse<ContactType>(request.Type, ignoreCase: true, out var type))
        {
            return Result.Failure<ContactResponse>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Type)));
        }

        var contact = Contact.Create(tenantId, type, request.Name, request.Document, request.Email, request.Phone, request.Notes);
        if (contact.IsFailure)
        {
            return Result.Failure<ContactResponse>(contact.Error);
        }

        contactRepository.Add(contact.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(contact.Value.ToResponse());
    }
}

public sealed class UpdateContactHandler(
    IContactRepository contactRepository,
    ICrmUnitOfWork unitOfWork) : ICommandHandler<UpdateContactCommand>
{
    public async Task<Result> Handle(UpdateContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await contactRepository.GetByIdAsync(ContactId.From(request.ContactId), cancellationToken);
        if (contact is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CONTACT_NOT_FOUND));
        }

        if (!Enum.TryParse<ContactType>(request.Type, ignoreCase: true, out var type))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Type)));
        }

        var result = contact.Update(type, request.Name, request.Document, request.Email, request.Phone, request.Notes);
        if (result.IsFailure)
        {
            return result;
        }

        contactRepository.Update(contact);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class DeactivateContactHandler(
    IContactRepository contactRepository,
    ICrmUnitOfWork unitOfWork) : ICommandHandler<DeactivateContactCommand>
{
    public async Task<Result> Handle(DeactivateContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await contactRepository.GetByIdAsync(ContactId.From(request.ContactId), cancellationToken);
        if (contact is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CONTACT_NOT_FOUND));
        }

        var result = contact.Deactivate();
        if (result.IsFailure)
        {
            return result;
        }

        contactRepository.Update(contact);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class ReactivateContactHandler(
    IContactRepository contactRepository,
    ICrmUnitOfWork unitOfWork) : ICommandHandler<ReactivateContactCommand>
{
    public async Task<Result> Handle(ReactivateContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await contactRepository.GetByIdAsync(ContactId.From(request.ContactId), cancellationToken);
        if (contact is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CONTACT_NOT_FOUND));
        }

        var result = contact.Reactivate();
        if (result.IsFailure)
        {
            return result;
        }

        contactRepository.Update(contact);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>404 (não 403) para contato de outro escritório: filtro global + RLS o tornam invisível.</summary>
public sealed class GetContactByIdHandler(IContactRepository contactRepository)
    : IQueryHandler<GetContactByIdQuery, ContactResponse>
{
    public async Task<Result<ContactResponse>> Handle(GetContactByIdQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactRepository.GetByIdAsync(ContactId.From(request.ContactId), cancellationToken);

        return contact is null
            ? Result.Failure<ContactResponse>(ErrorFactory.From(ErrorCode.CONTACT_NOT_FOUND))
            : Result.Success(contact.ToResponse());
    }
}

public sealed class ListContactsHandler(IContactRepository contactRepository, ICurrentUser currentUser)
    : IQueryHandler<ListContactsQuery, ListContactsResponse>
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    public async Task<Result<ListContactsResponse>> Handle(ListContactsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<ListContactsResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        ContactType? type = null;
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            if (!Enum.TryParse<ContactType>(request.Type, ignoreCase: true, out var parsed))
            {
                return Result.Failure<ListContactsResponse>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Type)));
            }

            type = parsed;
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : request.PageSize;

        var items = await contactRepository.ListAsync(
            tenantId, type, request.OnlyActive, request.Search, (page - 1) * pageSize, pageSize, cancellationToken);
        var totalCount = await contactRepository.CountAsync(
            tenantId, type, request.OnlyActive, request.Search, cancellationToken);

        return Result.Success(new ListContactsResponse(
            items.Select(contact => contact.ToResponse()).ToList(),
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize)));
    }
}
