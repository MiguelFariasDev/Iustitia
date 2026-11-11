using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Tags;

namespace Advocacia.Modules.Workflow.Application.Features.Tags;

public sealed class CreateTagHandler(
    ITagRepository repository,
    IWorkflowUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<CreateTagCommand, TagResponse>
{
    public async Task<Result<TagResponse>> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<TagResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var existing = string.IsNullOrWhiteSpace(request.Name)
            ? null
            : await repository.FindByNameAsync(tenantId, request.Name, cancellationToken);
        if (existing is not null)
        {
            return Result.Success(new TagResponse(existing.Id.Value, existing.Name, existing.Color));
        }

        var tag = Tag.Create(tenantId, request.Name, request.Color);
        if (tag.IsFailure)
        {
            return Result.Failure<TagResponse>(tag.Error);
        }

        repository.Add(tag.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TagResponse(tag.Value.Id.Value, tag.Value.Name, tag.Value.Color));
    }
}

public sealed class DeleteTagHandler(
    ITagRepository repository,
    IWorkflowUnitOfWork unitOfWork) : ICommandHandler<DeleteTagCommand>
{
    public async Task<Result> Handle(DeleteTagCommand request, CancellationToken cancellationToken)
    {
        var tagId = TagId.From(request.TagId);
        var tag = await repository.GetByIdAsync(tagId, cancellationToken);
        if (tag is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.TAG_NOT_FOUND));
        }

        await repository.RemoveAssignmentsOfTagAsync(tagId, cancellationToken);
        repository.Remove(tag);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class SetTagAssignmentHandler(
    ITagRepository repository,
    IProcessLookup processLookup,
    IWorkflowUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<SetTagAssignmentCommand>
{
    public async Task<Result> Handle(SetTagAssignmentCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        if (!Enum.TryParse<TagTarget>(request.Target, ignoreCase: true, out var target))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.TAG_TARGET_INVALID));
        }

        var tagId = TagId.From(request.TagId);
        if (await repository.GetByIdAsync(tagId, cancellationToken) is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.TAG_NOT_FOUND));
        }

        var existing = await repository.FindAssignmentAsync(tagId, target, request.TargetId, cancellationToken);

        if (request.Applied && existing is null)
        {
            if (target == TagTarget.Processo
                && !await processLookup.ExistsAsync(tenantId, request.TargetId, cancellationToken))
            {
                return Result.Failure(ErrorFactory.From(ErrorCode.WORK_TASK_PROCESS_NOT_FOUND));
            }

            repository.AddAssignment(new TagAssignment(tenantId, tagId, target, request.TargetId));
        }
        else if (!request.Applied && existing is not null)
        {
            repository.RemoveAssignment(existing);
        }
        else
        {
            return Result.Success();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class ListTagsHandler(ITagRepository repository, ICurrentUser currentUser)
    : IQueryHandler<ListTagsQuery, TagsResponse>
{
    public async Task<Result<TagsResponse>> Handle(ListTagsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<TagsResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var tags = await repository.ListAsync(tenantId, cancellationToken);
        var assignments = await repository.ListAssignmentsAsync(tenantId, cancellationToken);

        return Result.Success(new TagsResponse(
            tags.Select(tag => new TagResponse(tag.Id.Value, tag.Name, tag.Color)).ToList(),
            assignments.Select(a => new TagAssignmentResponse(a.TagId.Value, a.Target.ToString(), a.TargetId)).ToList()));
    }
}
