using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Drafting.Domain.Fila;

public enum QueueItemKind
{
    /// <summary>Gerar uma peça (referência = id da LegalDocumentRequest).</summary>
    Geracao = 0,

    /// <summary>Indexar um documento no banco vetorial (referência = id do IndexedDocument). Precisa da GPU para os embeddings.</summary>
    Indexacao = 1,
}

public enum QueueItemStatus
{
    Pendente = 0,
    Reservado = 1,
    Concluido = 2,
    Falhou = 3,
    Cancelado = 4,
}

/// <summary>
/// Item da fila persistente de trabalho que precisa de GPU. A fila fica no banco (sem Redis): é
/// durável, sobrevive a reinício e é consultável. Um item só é "do" worker enquanto a reserva
/// (<see cref="ReservadoAte"/>) estiver válida; reserva vencida (worker caiu) volta a ser elegível.
///
/// A tabela NÃO tem RLS, de propósito: ela só guarda ids e carimbos de tempo (nenhum dado do caso)
/// e o worker precisa enxergar todos os escritórios para agrupar o trabalho e ligar a GPU uma vez
/// só. O isolamento continua nas tabelas de dados (RLS) e no filtro de tenant do EF; a API nunca
/// expõe esta tabela diretamente.
/// </summary>
public sealed class GenerationQueueItem : Entity<Guid>, IHasTenant
{
    public const int ErroMaxLength = 500;
    public const int WorkerMaxLength = 100;

    public Guid TenantId { get; private set; }

    public QueueItemKind Tipo { get; private set; }

    public Guid ReferenciaId { get; private set; }

    public QueueItemStatus Status { get; private set; }

    /// <summary>Quantas vezes o item foi reservado para processar (inclui retentativas).</summary>
    public int Tentativas { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>Só é elegível a partir deste instante (backoff entre tentativas).</summary>
    public DateTimeOffset DisponivelEm { get; private set; }

    public string? ReservadoPor { get; private set; }

    public DateTimeOffset? ReservadoEm { get; private set; }

    public DateTimeOffset? ReservadoAte { get; private set; }

    public DateTimeOffset? ConcluidoEm { get; private set; }

    public string? UltimoErro { get; private set; }

    private GenerationQueueItem()
    {
        // EF Core.
    }

    public GenerationQueueItem(Guid tenantId, QueueItemKind tipo, Guid referenciaId, DateTimeOffset agora)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        Tipo = tipo;
        ReferenciaId = referenciaId;
        Status = QueueItemStatus.Pendente;
        CriadoEm = agora;
        DisponivelEm = agora;
    }
}
