using System.Text.Encodings.Web;
using System.Text.Unicode;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Movimentacoes;

namespace Advocacia.Modules.Legal.Application.Features.NotificacoesCliente;

internal static class NotificacaoClienteMapping
{
    /// <summary>Assunto genérico: o número do processo e os andamentos ficam no corpo, fora de logs de envio.</summary>
    internal const string Assunto = "Novidades no seu processo";

    /// <summary>Escapa &lt;, &gt;, &amp; e aspas, mas mantém os acentos legíveis (não vira &amp;#231;).</summary>
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    internal static NotificacaoClienteResponse ToResponse(this NotificacaoCliente notificacao, string? numeroProcesso) => new(
        notificacao.Id.Value,
        notificacao.ProcessoId,
        numeroProcesso,
        notificacao.ClienteId,
        notificacao.Status.ToString(),
        notificacao.Mensagem,
        notificacao.QuantidadeAndamentos,
        notificacao.CreatedAt,
        notificacao.RevisadoEm,
        notificacao.EnviadoEm,
        notificacao.FalhaEnvioEm);

    /// <summary>
    /// E-mail do aviso. O HTML é o texto aprovado ESCAPADO, linha a linha: a mensagem é digitada pelo
    /// advogado e contém texto do cartório, e nenhum dos dois pode virar marcação.
    /// </summary>
    internal static EmailMessage ToEmail(this NotificacaoCliente notificacao, string destinatario)
    {
        var linhas = notificacao.Mensagem.ReplaceLineEndings("\n").Split('\n');
        var html = string.Join("<br>", linhas.Select(linha => Html.Encode(linha)));

        return new EmailMessage(
            destinatario,
            Assunto,
            $"<div style=\"font-family:Arial,sans-serif;font-size:14px;line-height:1.5\">{html}</div>",
            notificacao.Mensagem);
    }
}
