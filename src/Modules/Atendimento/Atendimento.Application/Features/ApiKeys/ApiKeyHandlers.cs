using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using FluentValidation;

namespace Advocacia.Modules.Atendimentos.Application.Features.ApiKeys;

public sealed class IssueBotApiKeyValidator : AbstractValidator<IssueBotApiKeyCommand>
{
    public IssueBotApiKeyValidator() => RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
}

public sealed class IssueBotApiKeyHandler(
    IBotApiKeyRepository repository,
    IAtendimentoUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<IssueBotApiKeyCommand, IssuedBotApiKeyResponse>
{
    public async Task<Result<IssuedBotApiKeyResponse>> Handle(IssueBotApiKeyCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<IssuedBotApiKeyResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var (plain, prefix, hash) = BotApiKeyGenerator.Generate();
        var key = BotApiKey.Issue(tenantId, request.Name, prefix, hash, currentUser.UserId, clock.UtcNow);

        repository.Add(key);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new IssuedBotApiKeyResponse(key.Id, key.Name, key.Prefix, plain, key.CreatedAt));
    }
}

public sealed class RevokeBotApiKeyHandler(
    IBotApiKeyRepository repository,
    IAtendimentoUnitOfWork unitOfWork,
    IDateTimeProvider clock) : ICommandHandler<RevokeBotApiKeyCommand>
{
    public async Task<Result> Handle(RevokeBotApiKeyCommand request, CancellationToken cancellationToken)
    {
        var key = await repository.GetByIdAsync(request.KeyId, cancellationToken);
        if (key is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.ATENDIMENTO_BOT_API_KEY_NOT_FOUND));
        }

        key.Revoke(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class ListBotApiKeysHandler(IBotApiKeyRepository repository)
    : IQueryHandler<ListBotApiKeysQuery, IReadOnlyList<BotApiKeyResponse>>
{
    public async Task<Result<IReadOnlyList<BotApiKeyResponse>>> Handle(
        ListBotApiKeysQuery request, CancellationToken cancellationToken)
    {
        var keys = await repository.ListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BotApiKeyResponse>>(keys
            .Select(key => new BotApiKeyResponse(
                key.Id, key.Name, key.Prefix, key.IsActive, key.CreatedAt, key.LastUsedAt, key.RevokedAt))
            .ToList());
    }
}
