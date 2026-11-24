namespace Advocacia.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Porta de envio de e-mail transacional, independente de provedor. Falha transitória (rede, 5xx,
/// 429) LANÇA exceção — quem chama é um consumer de mensageria, e o MassTransit retenta com
/// backoff. Nunca receber nem registrar o corpo da mensagem em log: pode citar clientes e prazos.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <param name="To">Destinatário único (e-mails transacionais nunca vão em cópia para terceiros).</param>
/// <param name="Subject">Assunto.</param>
/// <param name="Html">Corpo em HTML, já escapado.</param>
/// <param name="Text">Alternativa em texto puro (acessibilidade e filtros de spam).</param>
public sealed record EmailMessage(string To, string Subject, string Html, string Text);
