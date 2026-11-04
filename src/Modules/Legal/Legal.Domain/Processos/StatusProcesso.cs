namespace Advocacia.Modules.Legal.Domain.Processos;

/// <summary>
/// Situação do processo no acompanhamento do escritório — NÃO é a fase processual oficial
/// (essa vem do tribunal). Só <see cref="Ativo"/> e <see cref="Suspenso"/> continuam sendo
/// consultados no diário; ver CnjCaptureJob.
/// </summary>
public enum StatusProcesso
{
    /// <summary>Em andamento e sob acompanhamento — gera captura de publicações.</summary>
    Ativo,

    /// <summary>Suspenso ou sobrestado. Ainda acompanhado: pode voltar a publicar a qualquer momento.</summary>
    Suspenso,

    /// <summary>Encerrado com trânsito em julgado ou acordo. Não gera mais captura.</summary>
    Encerrado,

    /// <summary>Arquivado pelo escritório. Estado terminal, mantido para histórico.</summary>
    Arquivado,
}
