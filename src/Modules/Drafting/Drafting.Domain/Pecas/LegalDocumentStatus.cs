namespace Advocacia.Modules.Drafting.Domain.Pecas;

/// <summary>Ciclo de vida de uma solicitação: Pendente → Processando → Concluido | Falhou.</summary>
public enum LegalDocumentStatus
{
    Pendente = 0,
    Processando = 1,
    Concluido = 2,
    Falhou = 3,
}
