using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Platform.Infrastructure.Notifications.EmailService;

/// <summary>
/// Envia pela API do Resend (<c>POST /emails</c>, Bearer). Sem chave/remetente configurados, só
/// registra que deixou de enviar (sem destinatário nem corpo) — dev local e CI não precisam de
/// provedor. Falhas transitórias lançam para o MassTransit retentar; 4xx (chave inválida,
/// remetente não verificado, destinatário rejeitado) também lançam, para a falha aparecer no
/// dead-letter em vez de sumir.
/// </summary>
public sealed class ResendEmailSender(
    HttpClient httpClient, IOptions<EmailOptions> options, ILogger<ResendEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            logger.LogInformation("E-mail não configurado (Email:ApiKey/FromAddress) — envio ignorado: {Subject}.", message.Subject);

            return;
        }

        var baseUri = new Uri(settings.ApiBaseUrl.TrimEnd('/') + "/");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "emails"))
        {
            Content = JsonContent.Create(new
            {
                from = $"{settings.FromName} <{settings.FromAddress}>",
                to = new[] { message.To },
                subject = message.Subject,
                html = message.Html,
                text = message.Text,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var status = (int)response.StatusCode;
        logger.LogWarning("Provedor de e-mail respondeu HTTP {Status}.", status);

        // O corpo da resposta pode ecoar o destinatário; fica fora da exceção e do log.
        throw new HttpRequestException($"O provedor de e-mail respondeu HTTP {status}.", inner: null, response.StatusCode);
    }
}
