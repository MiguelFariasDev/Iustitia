using System.Net;

namespace Advocacia.Platform.Infrastructure.Notifications.EmailService;

/// <summary>
/// Layout dos e-mails transacionais (marca Iustitia: azul de ação, tipografia de sistema porque
/// clientes de e-mail não carregam fontes web). Só entra o TÍTULO do alerta e um link para o app:
/// descrição, partes e número de processo ficam dentro da plataforma — e-mail não é canal seguro
/// para sigilo profissional (OAB) e a caixa postal pode ser compartilhada.
/// </summary>
public static class EmailTemplates
{
    public static (string Subject, string Html, string Text) Alert(string title, string link)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeLink = WebUtility.HtmlEncode(link);
        var subject = $"Iustitia: {title}";

        var html = $"""
            <!doctype html>
            <html lang="pt-BR"><body style="margin:0;background:#f4f1ea;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#1b2430">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center" style="padding:32px 16px">
              <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border:1px solid #dcd6c8;border-radius:12px">
                <tr><td style="padding:28px 32px 8px;font-size:20px;font-weight:700;color:#1a4fd0">Iustitia</td></tr>
                <tr><td style="padding:8px 32px 0;font-size:16px;line-height:1.5">Você tem um novo alerta:</td></tr>
                <tr><td style="padding:12px 32px 0;font-size:18px;font-weight:600;line-height:1.4">{safeTitle}</td></tr>
                <tr><td style="padding:24px 32px 8px"><a href="{safeLink}" style="display:inline-block;background:#1a4fd0;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:600">Abrir na plataforma</a></td></tr>
                <tr><td style="padding:20px 32px 28px;font-size:12px;line-height:1.5;color:#5b6573">Por segurança, os detalhes ficam dentro da plataforma. Este é um aviso automático da plataforma.</td></tr>
              </table>
            </td></tr></table></body></html>
            """;

        var text = $"Iustitia\n\nVocê tem um novo alerta: {title}\n\nAbrir na plataforma: {link}\n\n" +
                   "Por segurança, os detalhes ficam dentro da plataforma.";

        return (subject, html, text);
    }
}
