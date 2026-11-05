using System.Collections.Concurrent;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Atendimentos.Application.Abstractions;

namespace Advocacia.Platform.IntegrationTests.Common;

/// <summary>
/// Faz o papel do Bot nos testes do Core: registra o que o Core mandaria entregar ao WhatsApp.
/// O contrato real entre Core e Bot é coberto no repositório do Bot (BotSendEndpointTests).
/// </summary>
public sealed class FakeBotGateway : IBotGateway
{
    public sealed record SentMessage(Guid TenantId, Guid AtendimentoId, Guid MessageId, string To, string Body);

    public ConcurrentQueue<SentMessage> Sent { get; } = new();

    /// <summary>Quando definido, o próximo envio falha — simula o Bot fora do ar.</summary>
    public bool FailNext { get; set; }

    public Task<Result<string?>> SendTextAsync(
        Guid tenantId, Guid atendimentoId, Guid mensagemId, string phoneNumber, string body, CancellationToken cancellationToken = default)
    {
        if (FailNext)
        {
            FailNext = false;
            return Task.FromResult(Result.Failure<string?>(Error.Failure("BOT_UNREACHABLE", "Bot fora do ar.")));
        }

        Sent.Enqueue(new SentMessage(tenantId, atendimentoId, mensagemId, phoneNumber, body));
        return Task.FromResult(Result.Success<string?>($"wamid.out.{mensagemId:N}"));
    }
}
