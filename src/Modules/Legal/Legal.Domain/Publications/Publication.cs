using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Domain.Events;
using Advocacia.Modules.Legal.Domain.Shared;

namespace Advocacia.Modules.Legal.Domain.Publications;

/// <summary>
/// Publicação capturada de um diário oficial (hoje, o DJEN/CNJ) e atribuída a um
/// escritório. É a raiz de agregado do ciclo de revisão humana: nasce em
/// <see cref="PublicationStatus.PendingReview"/> e só muda de estado por ação explícita de
/// um advogado (Provimento CFOAB nº 271/2025 — ver ADR-012).
///
/// A deduplicação é por <see cref="ExternalId"/> dentro do tenant, garantida por índice
/// único composto (tenant_id, external_id) — ver ADR-056. A checagem em código é a
/// primeira camada; o índice é a que realmente impede a duplicata sob concorrência.
/// </summary>
public sealed class Publication : AggregateRoot<PublicationId>, IAuditable, ISoftDeletable, IHasTenant
{
    public Guid TenantId { get; private set; }

    /// <summary>
    /// Processo ao qual a publicação pertence. Nulo na Fase 1: a captura acontece antes de
    /// existir cadastro de processos (Fase 2), então a publicação é persistida "solta" e
    /// vinculada depois por <see cref="AttachToProcess"/>.
    /// </summary>
    public Guid? ProcessId { get; private set; }

    /// <summary>Cliente do processo ao qual a publicação pertence — denormalizado na captura para filtrar sem join.</summary>
    public Guid? ClienteId { get; private set; }

    /// <summary>Advogado responsável pelo processo no momento da captura: é quem recebe o alerta.</summary>
    public Guid? ResponsavelUserId { get; private set; }

    public CNJNumber CnjNumber { get; private set; } = null!;

    /// <summary>
    /// Colunas DERIVADAS de CnjNumber/PublishedAt. Existem porque o EF Core não traduz para SQL
    /// consultas que alcançam o interior de um value object convertido (ver QueryTranslationTests):
    /// filtrar por tribunal, período e número parcial precisa de colunas planas e indexáveis.
    /// </summary>
    public string? Tribunal { get; private set; }

    public string CnjDigits { get; private set; } = string.Empty;

    public DateOnly PublishedOn { get; private set; }

    /// <summary>Identificador da publicação na origem (id da comunicação no DJEN). Chave de deduplicação.</summary>
    public string ExternalId { get; private set; } = null!;

    public PublicationSource Source { get; private set; }

    /// <summary>Inteiro teor normalizado. NUNCA logar (ver <see cref="RawContent"/>).</summary>
    public RawContent RawContent { get; private set; } = null!;

    public PublishedAt PublishedAt { get; private set; } = null!;

    /// <summary>Quando o Iustitia capturou a publicação — distinto de <see cref="PublishedAt"/>, que é a data do diário.</summary>
    public DateTimeOffset CapturedAt { get; private set; }

    public PublicationType? Type { get; private set; }

    public PublicationStatus Status { get; private set; }

    public bool IsRead { get; private set; }

    /// <summary>Metadados extras da origem (órgão, classe, link, hash), em JSONB.</summary>
    public string? MetadataJson { get; private set; }

    /// <summary>Anotação da última mudança de status — o "porquê" da decisão do advogado.</summary>
    public string? ReviewNotes { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    private Publication()
    {
        // EF Core.
    }

    private Publication(
        PublicationId id,
        Guid tenantId,
        CNJNumber cnjNumber,
        string externalId,
        PublicationSource source,
        RawContent rawContent,
        PublishedAt publishedAt,
        DateTimeOffset capturedAt,
        PublicationType? type,
        string? metadataJson)
        : base(id)
    {
        TenantId = tenantId;
        CnjNumber = cnjNumber;
        CnjDigits = cnjNumber.Digits;
        Tribunal = cnjNumber.Tribunal;
        ExternalId = externalId;
        Source = source;
        RawContent = rawContent;
        PublishedAt = publishedAt;
        PublishedOn = publishedAt.ToDateOnly();
        CapturedAt = capturedAt;
        Type = type;
        MetadataJson = metadataJson;
        Status = PublicationStatus.PendingReview;
        IsRead = false;
    }

    /// <summary>
    /// Cria uma publicação a partir de dados já capturados. Os value objects
    /// (<see cref="CNJNumber"/>, <see cref="RawContent"/>, <see cref="PublishedAt"/>) são
    /// construídos pelo chamador, que já trata as falhas de formato individualmente — aqui
    /// só validam-se as invariantes que dependem do conjunto.
    /// </summary>
    public static Result<Publication> Create(
        Guid tenantId,
        CNJNumber cnjNumber,
        string externalId,
        PublicationSource source,
        RawContent rawContent,
        PublishedAt publishedAt,
        DateTimeOffset capturedAt,
        PublicationType? type = null,
        string? metadataJson = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Publication>(
                ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId)));
        }

        if (string.IsNullOrWhiteSpace(externalId))
        {
            return Result.Failure<Publication>(
                ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(ExternalId)));
        }

        if (cnjNumber is null)
        {
            return Result.Failure<Publication>(ErrorFactory.From(ErrorCode.PUBLICATION_INVALID_CNJ));
        }

        if (rawContent is null)
        {
            return Result.Failure<Publication>(ErrorFactory.From(ErrorCode.PUBLICATION_EMPTY_CONTENT));
        }

        if (publishedAt is null)
        {
            return Result.Failure<Publication>(ErrorFactory.From(ErrorCode.PUBLICATION_INVALID_DATE));
        }

        var publication = new Publication(
            PublicationId.New(),
            tenantId,
            cnjNumber,
            externalId.Trim(),
            source,
            rawContent,
            publishedAt,
            capturedAt.ToUniversalTime(),
            type,
            metadataJson);

        publication.RaiseDomainEvent(new PublicationCapturedEvent(
            publication.Id.Value,
            publication.TenantId,
            publication.CnjNumber.Value,
            publication.ExternalId,
            publication.Source,
            publication.PublishedAt.Value));

        return Result.Success(publication);
    }

    public Result MarkAsRead()
    {
        if (IsRead)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PUBLICATION_ALREADY_READ));
        }

        IsRead = true;

        return Result.Success();
    }

    /// <summary>
    /// Muda o status de revisão. As transições permitidas estão em
    /// <see cref="CanTransitionTo"/>: nenhuma publicação volta para
    /// <see cref="PublicationStatus.PendingReview"/> depois de revisada, e
    /// <see cref="PublicationStatus.Archived"/> é terminal — o histórico da decisão do
    /// advogado é imutável por exigência de auditoria.
    /// </summary>
    public Result ChangeStatus(PublicationStatus newStatus, string? notes = null)
    {
        if (newStatus == Status)
        {
            return Result.Failure(
                ErrorFactory.From(ErrorCode.PUBLICATION_INVALID_STATUS_TRANSITION, Status, newStatus));
        }

        if (!CanTransitionTo(Status, newStatus))
        {
            return Result.Failure(
                ErrorFactory.From(ErrorCode.PUBLICATION_INVALID_STATUS_TRANSITION, Status, newStatus));
        }

        var previousStatus = Status;
        Status = newStatus;
        ReviewNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        RaiseDomainEvent(new PublicationStatusChangedEvent(Id.Value, TenantId, previousStatus, newStatus, ReviewNotes));

        return Result.Success();
    }

    /// <summary>Vincula a publicação a um processo cadastrado (Fase 2). Idempotente para o mesmo processo.</summary>
    public Result AttachToProcess(Guid processId)
    {
        if (processId == Guid.Empty)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(ProcessId)));
        }

        if (ProcessId == processId)
        {
            return Result.Success();
        }

        if (ProcessId is not null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.COMMON_INVALID_OPERATION));
        }

        ProcessId = processId;

        return Result.Success();
    }

    /// <summary>
    /// Vincula a publicação ao processo cadastrado e, junto, ao cliente e ao responsável dele.
    /// É o caminho da captura dirigida por processo: os três vêm do mesmo cadastro, então
    /// entram juntos. Idempotente para o mesmo processo.
    /// </summary>
    public Result LinkTo(Guid processId, Guid? clienteId, Guid? responsavelUserId)
    {
        var attach = AttachToProcess(processId);
        if (attach.IsFailure)
        {
            return attach;
        }

        ClienteId = clienteId == Guid.Empty ? null : clienteId;
        ResponsavelUserId = responsavelUserId == Guid.Empty ? null : responsavelUserId;

        return Result.Success();
    }

    /// <summary>
    /// Registra o tipo do ato. Separado do <see cref="Create"/> porque na captura o tipo
    /// costuma vir vazio do CNJ: a classificação assistida por IA é da Fase 3 e, quando
    /// chegar, só grava aqui DEPOIS da confirmação do advogado (ver ADR-012).
    /// </summary>
    public void SetType(PublicationType type) => Type = type;

    private static bool CanTransitionTo(PublicationStatus from, PublicationStatus to) => from switch
    {
        PublicationStatus.PendingReview =>
            to is PublicationStatus.Approved
                or PublicationStatus.Corrected
                or PublicationStatus.Rejected
                or PublicationStatus.Archived,
        PublicationStatus.Approved or PublicationStatus.Corrected or PublicationStatus.Rejected =>
            to is PublicationStatus.Archived,
        PublicationStatus.Archived => false,
        _ => false,
    };
}
