using System.Globalization;
using System.Text;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Legal.Domain.Movimentacoes;

/// <summary>
/// Aviso ao cliente sobre novidades no processo (ADR-075). Nasce como RASCUNHO do acompanhamento
/// noturno e só é enviado depois que o advogado revisa — revisão humana obrigatória (Provimento
/// CFOAB nº 271/2025; claude.md, princípio 1). O banco reforça: um aviso Enviada sem revisor ou
/// sem data de envio viola um CHECK.
///
/// Há no máximo UM rascunho pendente por processo (índice único parcial): os andamentos das noites
/// seguintes se somam a ele, em vez de empilhar avisos que o advogado teria de revisar um a um.
/// </summary>
public sealed class NotificacaoCliente : AggregateRoot<NotificacaoClienteId>, IHasTenant, IAuditable
{
    public const int MensagemMaxLength = 8000;

    /// <summary>Quantos andamentos o rascunho lista; acima disso, "e mais N".</summary>
    public const int MaxAndamentosListados = 20;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public Guid TenantId { get; private set; }

    public Guid ProcessoId { get; private set; }

    public Guid ClienteId { get; private set; }

    public StatusNotificacaoCliente Status { get; private set; }

    /// <summary>Texto do aviso: o rascunho gerado ou, depois do envio, o texto que o advogado aprovou.</summary>
    public string Mensagem { get; private set; } = string.Empty;

    public int QuantidadeAndamentos { get; private set; }

    /// <summary>
    /// Captura que originou o rascunho. Os andamentos do aviso são todos os capturados a partir
    /// daqui — é assim que as noites seguintes se somam a ele.
    /// </summary>
    public DateTimeOffset AndamentosDesde { get; private set; }

    public Guid? RevisadoPor { get; private set; }

    public DateTimeOffset? RevisadoEm { get; private set; }

    public DateTimeOffset? EnviadoEm { get; private set; }

    public DateTimeOffset? FalhaEnvioEm { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    private NotificacaoCliente()
    {
        // EF Core.
    }

    private NotificacaoCliente(NotificacaoClienteId id)
        : base(id)
    {
    }

    public bool Pendente => Status == StatusNotificacaoCliente.PendenteRevisao;

    /// <param name="numeroProcesso">Número CNJ mascarado.</param>
    /// <param name="andamentos">Andamentos novos, em qualquer ordem.</param>
    /// <param name="capturadosEm">Momento da captura dos andamentos (o mesmo gravado neles).</param>
    public static NotificacaoCliente CriarRascunho(
        Guid tenantId,
        Guid processoId,
        Guid clienteId,
        string numeroProcesso,
        IReadOnlyCollection<AndamentoDoAviso> andamentos,
        DateTimeOffset capturadosEm) =>
        new(NotificacaoClienteId.New())
        {
            TenantId = tenantId,
            ProcessoId = processoId,
            ClienteId = clienteId,
            Status = StatusNotificacaoCliente.PendenteRevisao,
            QuantidadeAndamentos = andamentos.Count,
            AndamentosDesde = capturadosEm,
            Mensagem = MontarMensagem(numeroProcesso, andamentos),
        };

    /// <summary>
    /// Soma os andamentos de uma nova noite ao rascunho ainda pendente. O texto é refeito com a lista
    /// completa: o rascunho é do sistema até o advogado mexer nele, e a edição só acontece no envio.
    /// </summary>
    public Result Acumular(string numeroProcesso, IReadOnlyCollection<AndamentoDoAviso> todosOsAndamentos)
    {
        if (!Pendente)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PROCESS_CLIENT_NOTIFICATION_NOT_PENDING));
        }

        QuantidadeAndamentos = todosOsAndamentos.Count;
        Mensagem = MontarMensagem(numeroProcesso, todosOsAndamentos);

        return Result.Success();
    }

    /// <summary>
    /// O advogado revisou e mandou enviar. Registra quem e quando, e fixa o texto final
    /// (<paramref name="mensagemEditada"/> ou o rascunho). O envio em si vem depois:
    /// <see cref="MarcarEnviada"/> ou <see cref="MarcarFalhaEnvio"/>.
    /// </summary>
    public Result Aprovar(Guid revisor, string? mensagemEditada, DateTimeOffset agora)
    {
        if (Status is not (StatusNotificacaoCliente.PendenteRevisao or StatusNotificacaoCliente.FalhaEnvio))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PROCESS_CLIENT_NOTIFICATION_NOT_PENDING));
        }

        if (revisor == Guid.Empty)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(RevisadoPor)));
        }

        var texto = mensagemEditada is null ? Mensagem : mensagemEditada.Trim();
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > MensagemMaxLength)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PROCESS_CLIENT_NOTIFICATION_MESSAGE_INVALID));
        }

        Mensagem = texto;
        RevisadoPor = revisor;
        RevisadoEm = agora;

        return Result.Success();
    }

    public Result MarcarEnviada(DateTimeOffset agora)
    {
        // Nunca Enviada sem revisão: o CHECK do banco também exige revisor e data de envio.
        if (RevisadoPor is null || Status is not (StatusNotificacaoCliente.PendenteRevisao or StatusNotificacaoCliente.FalhaEnvio))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PROCESS_CLIENT_NOTIFICATION_NOT_PENDING));
        }

        Status = StatusNotificacaoCliente.Enviada;
        EnviadoEm = agora;

        return Result.Success();
    }

    public void MarcarFalhaEnvio(DateTimeOffset agora)
    {
        Status = StatusNotificacaoCliente.FalhaEnvio;
        FalhaEnvioEm = agora;
    }

    public Result Descartar(Guid revisor, DateTimeOffset agora)
    {
        if (Status is not (StatusNotificacaoCliente.PendenteRevisao or StatusNotificacaoCliente.FalhaEnvio))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PROCESS_CLIENT_NOTIFICATION_NOT_PENDING));
        }

        Status = StatusNotificacaoCliente.Descartada;
        RevisadoPor = revisor;
        RevisadoEm = agora;

        return Result.Success();
    }

    /// <summary>
    /// Texto do rascunho. Sem nome do cliente de propósito: o tratamento fica com o advogado, que
    /// sabe como se dirige a cada cliente — e o rascunho não guarda dado pessoal além do necessário.
    /// </summary>
    internal static string MontarMensagem(string numeroProcesso, IReadOnlyCollection<AndamentoDoAviso> andamentos)
    {
        var ordenados = andamentos.OrderByDescending(a => a.DataHora ?? DateTimeOffset.MinValue).ToList();
        var texto = new StringBuilder();
        texto.AppendLine("Prezado(a) cliente,");
        texto.AppendLine();
        texto.AppendLine(ordenados.Count == 1
            ? $"Houve uma novidade no seu processo nº {numeroProcesso}:"
            : $"Houve {ordenados.Count} novidades no seu processo nº {numeroProcesso}:");
        texto.AppendLine();

        foreach (var andamento in ordenados.Take(MaxAndamentosListados))
        {
            var data = andamento.DataHora is { } quando
                ? quando.ToOffset(TimeSpan.FromHours(-3)).ToString("dd/MM/yyyy", PtBr)
                : "sem data";
            texto.AppendLine($"- {data}: {andamento.Descricao}");
        }

        if (ordenados.Count > MaxAndamentosListados)
        {
            texto.AppendLine($"- e mais {ordenados.Count - MaxAndamentosListados} andamento(s).");
        }

        texto.AppendLine();
        texto.Append("Qualquer dúvida, estamos à disposição.");

        var mensagem = texto.ToString();

        return mensagem.Length <= MensagemMaxLength ? mensagem : mensagem[..MensagemMaxLength];
    }
}

/// <summary>Andamento como aparece no aviso: data e descrição (nome e, quando houver, complemento).</summary>
public sealed record AndamentoDoAviso(DateTimeOffset? DataHora, string Descricao);
