using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Atendimentos.Application.Features.ApiKeys;

/// <summary>Emite a chave do Bot do escritório. A chave em claro só existe na resposta desta chamada.</summary>
public sealed record IssueBotApiKeyCommand(string Name) : ICommand<IssuedBotApiKeyResponse>;

public sealed record RevokeBotApiKeyCommand(Guid KeyId) : ICommand;

public sealed record ListBotApiKeysQuery : IQuery<IReadOnlyList<BotApiKeyResponse>>;

public sealed record IssuedBotApiKeyResponse(Guid Id, string Name, string Prefix, string ApiKey, DateTimeOffset CreatedAt);

public sealed record BotApiKeyResponse(
    Guid Id,
    string Name,
    string Prefix,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);
