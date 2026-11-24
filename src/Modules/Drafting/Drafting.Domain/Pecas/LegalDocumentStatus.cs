namespace Advocacia.Modules.Drafting.Domain.Pecas;

/// <summary>
/// Ciclo de vida de uma solicitação de peça.
///
/// Pendente (registrada e na fila) → NaFila (reservada por um worker) → PreparandoGpu (subindo a
/// instância / esperando o Ollama) → BuscandoContexto (RAG) → Gerando → Salvando → Concluido.
/// Fora do caminho feliz: Falhou (esgotou tentativas ou erro permanente) e Cancelado (pelo usuário).
/// Falha transitória volta a Pendente para nova tentativa.
/// </summary>
public enum LegalDocumentStatus
{
    Pendente = 0,
    NaFila = 1,
    PreparandoGpu = 2,
    BuscandoContexto = 3,
    Gerando = 4,
    Salvando = 5,
    Concluido = 6,
    Falhou = 7,
    Cancelado = 8,
}
