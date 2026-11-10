namespace Advocacia.Modules.Atendimentos.Domain.Atendimentos;

/// <summary>
/// Quem está conduzindo a conversa. É o que o Bot consulta antes de responder qualquer
/// mensagem: só <see cref="Bot"/> autoriza resposta automática.
/// </summary>
public enum AtendimentoStatus
{
    /// <summary>Fluxo automático ativo.</summary>
    Bot,

    /// <summary>O cliente pediu um humano e ninguém assumiu ainda. O bot fica em silêncio.</summary>
    PendingHuman,

    /// <summary>Um advogado assumiu. O bot NUNCA responde.</summary>
    Human,

    /// <summary>Encerrado. Uma nova mensagem do mesmo telefone abre outro atendimento.</summary>
    Closed,
}

public enum MessageDirection
{
    Inbound,
    Outbound,
}

public enum MessageSender
{
    Client,
    Bot,
    Human,
}

/// <summary>Situação do envio ao WhatsApp. Só faz sentido para mensagens de saída.</summary>
public enum DeliveryStatus
{
    Pending,
    Sent,
    Failed,
}
