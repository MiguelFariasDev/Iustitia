using System.Net.Http.Json;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Atendimentos.Infrastructure.Bot;

public sealed class BotGatewayOptions
{
    public const string SectionName = "Atendimento:Bot";

    /// <summary>URL base do Bot (ex.: https://bot.interno). Vazia = Bot não configurado.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Chave que o Bot espera do Core. É OUTRA credencial, independente da chave que o Bot usa para chamar o Core.</summary>
    public string? ApiKey { get; set; }
}

/// <summary>
/// Entrega a mensagem do advogado ao Bot (<c>POST /api/v1/bot/send</c>), que a repassa à Meta.
/// O Core nunca fala com a Meta (regra de isolamento).
///
/// Telefone e corpo da mensagem são dado pessoal e sigiloso: nunca vão para o log.
/// </summary>
public sealed class BotGateway(HttpClient httpClient, IOptions<BotGatewayOptions> options, ILogger<BotGateway> logger)
    : IBotGateway
{
    public async Task<Result<string?>> SendTextAsync(
        Guid tenantId,
        Guid atendimentoId,
        Guid mensagemId,
        string phoneNumber,
        string body,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.BaseUrl) || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            logger.LogWarning("Bot não configurado (Atendimento:Bot:BaseUrl/ApiKey) — mensagem {MessageId} não enviada.", mensagemId);
            return Result.Failure<string?>(Error.Failure("BOT_NOT_CONFIGURED", "O Bot não está configurado."));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl.TrimEnd('/')}/api/v1/bot/send")
        {
            // messageId é a chave de idempotência do Bot: um retry nunca envia duas vezes ao cliente.
            Content = JsonContent.Create(new { tenantId, atendimentoId, messageId = mensagemId, to = phoneNumber, body }),
        };
        request.Headers.Add("X-Api-Key", settings.ApiKey);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Bot respondeu {StatusCode} ao enviar a mensagem {MessageId}.", (int)response.StatusCode, mensagemId);
                return Result.Failure<string?>(Error.Failure("BOT_SEND_FAILED", "O Bot recusou o envio."));
            }

            var payload = await response.Content.ReadFromJsonAsync<BotSendResponse>(cancellationToken);
            return Result.Success(payload?.ExternalId);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Falha de comunicação com o Bot ao enviar a mensagem {MessageId}.", mensagemId);
            return Result.Failure<string?>(Error.Failure("BOT_UNREACHABLE", "Não foi possível falar com o Bot."));
        }
    }

    private sealed record BotSendResponse(string? ExternalId);
}
