namespace Advocacia.Modules.Drafting.Domain.Pecas;

/// <summary>
/// Ciclo de vida de uma solicitação de peça.
///
/// Pendente (registrada e na fila) → NaFila (reservada por um worker) → PreparandoGpu (subindo a
/// instância / esperando o Ollama) → BuscandoDocumentosCaso (anexos) → BuscandoJuridico (leis e
/// jurisprudência) → Gerando → Validando (citações) → Salvando → Concluido.
/// Fora do caminho feliz: Falhou (esgotou tentativas ou erro permanente) e Cancelado (pelo usuário).
/// Falha transitória volta a Pendente para nova tentativa.
/// </summary>
public enum LegalDocumentStatus
{
    Pendente = 0,
    NaFila = 1,
    PreparandoGpu = 2,

    /// <summary>Legado (busca única). Mantido só para ler linhas antigas; nada novo usa este valor.</summary>
    BuscandoContexto = 3,
    Gerando = 4,
    Salvando = 5,
    Concluido = 6,
    Falhou = 7,
    Cancelado = 8,
    BuscandoDocumentosCaso = 9,
    BuscandoJuridico = 10,
    Validando = 11,
}
