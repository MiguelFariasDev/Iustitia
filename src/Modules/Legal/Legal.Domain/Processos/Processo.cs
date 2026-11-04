using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Domain.Events;
using Advocacia.Modules.Legal.Domain.Shared;

namespace Advocacia.Modules.Legal.Domain.Processos;

/// <summary>
/// Processo judicial acompanhado pelo escritório. É o agregado que DIRIGE a captura de
/// publicações: o job consulta o DJEN pelos números dos processos cadastrados e ativos, e
/// não por varredura de tribunal (ver CnjCaptureJob e docs/modules/legal/cnj-capture.md).
/// Nenhuma publicação entra na base do escritório sem um processo dele que a justifique.
///
/// Referencia o cliente e o responsável por ID, nunca por objeto: Cliente é outro agregado e
/// o usuário responsável pertence ao módulo Platform, que este módulo não conhece (ADR-055).
/// </summary>
public sealed class Processo : AggregateRoot<ProcessoId>, IAuditable, ISoftDeletable, IHasTenant
{
    public Guid TenantId { get; private set; }

    public CNJNumber CnjNumber { get; private set; } = null!;

    /// <summary>
    /// Os 20 dígitos do número CNJ, sem máscara. DERIVADO de <see cref="CnjNumber"/> — existe
    /// pelo mesmo motivo de Cliente.NomeBusca: busca "contém" não traduz sobre value object
    /// convertido. Também é o formato que o DJEN espera no parâmetro numeroProcesso, então
    /// o job usa esta coluna direto, sem reprocessar a máscara.
    /// </summary>
    public string CnjDigits { get; private set; } = string.Empty;

    public Guid ClienteId { get; private set; }

    /// <summary>Usuário do escritório responsável (Platform.Domain.Identity.User) — por id.</summary>
    public Guid ResponsavelUserId { get; private set; }

    public AreaDireito Area { get; private set; }

    public StatusProcesso Status { get; private set; }

    public DadosForo Foro { get; private set; } = DadosForo.Vazio;

    public string? Assunto { get; private set; }

    public string? ParteContraria { get; private set; }

    /// <summary>Valor da causa em reais. Nulo quando não informado ou inestimável.</summary>
    public decimal? ValorCausa { get; private set; }

    public DateOnly? DataDistribuicao { get; private set; }

    public string? Observacoes { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    /// <summary>
    /// Se este processo deve ser consultado no diário. Suspenso continua sendo acompanhado —
    /// um processo sobrestado volta a publicar sem aviso, e deixar de consultá-lo é
    /// exatamente como se perde um prazo.
    /// </summary>
    public bool GeraCaptura => !IsDeleted && Status is StatusProcesso.Ativo or StatusProcesso.Suspenso;

    private Processo()
    {
        // EF Core.
    }

    private Processo(ProcessoId id, Guid tenantId, CNJNumber cnjNumber, Guid clienteId, Guid responsavelUserId, AreaDireito area)
        : base(id)
    {
        TenantId = tenantId;
        CnjNumber = cnjNumber;
        CnjDigits = cnjNumber.Digits;
        ClienteId = clienteId;
        ResponsavelUserId = responsavelUserId;
        Area = area;
        Status = StatusProcesso.Ativo;
    }

    public static Result<Processo> Create(
        Guid tenantId,
        CNJNumber cnjNumber,
        Guid clienteId,
        Guid responsavelUserId,
        AreaDireito area,
        DadosForo? foro = null,
        string? assunto = null,
        string? parteContraria = null,
        decimal? valorCausa = null,
        DateOnly? dataDistribuicao = null,
        string? observacoes = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Processo>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId)));
        }

        if (cnjNumber is null)
        {
            return Result.Failure<Processo>(ErrorFactory.From(ErrorCode.PROCESS_CNJ_INVALID));
        }

        if (clienteId == Guid.Empty)
        {
            return Result.Failure<Processo>(ErrorFactory.From(ErrorCode.PROCESS_CLIENT_REQUIRED));
        }

        if (responsavelUserId == Guid.Empty)
        {
            return Result.Failure<Processo>(ErrorFactory.From(ErrorCode.PROCESS_RESPONSIBLE_REQUIRED));
        }

        if (valorCausa is < 0)
        {
            return Result.Failure<Processo>(ErrorFactory.From(ErrorCode.VALIDATION_OUT_OF_RANGE, nameof(ValorCausa)));
        }

        var processo = new Processo(ProcessoId.New(), tenantId, cnjNumber, clienteId, responsavelUserId, area)
        {
            Foro = foro ?? DadosForo.Vazio,
            Assunto = Normalizar(assunto),
            ParteContraria = Normalizar(parteContraria),
            ValorCausa = valorCausa,
            DataDistribuicao = dataDistribuicao,
            Observacoes = Normalizar(observacoes),
        };

        processo.RaiseDomainEvent(
            new ProcessoCriadoEvent(processo.Id.Value, tenantId, clienteId, cnjNumber.Value));

        return Result.Success(processo);
    }

    /// <summary>
    /// Atualiza os dados cadastrais. O NÚMERO CNJ não é alterável: é a identidade do processo
    /// perante o Judiciário e a chave pela qual as publicações já capturadas foram vinculadas.
    /// Trocá-lo órfãos essas publicações sem deixar rastro. Cadastrou errado, cadastra de novo.
    /// </summary>
    public Result Update(
        Guid clienteId,
        Guid responsavelUserId,
        AreaDireito area,
        DadosForo foro,
        string? assunto,
        string? parteContraria,
        decimal? valorCausa,
        DateOnly? dataDistribuicao,
        string? observacoes)
    {
        if (clienteId == Guid.Empty)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PROCESS_CLIENT_REQUIRED));
        }

        if (responsavelUserId == Guid.Empty)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PROCESS_RESPONSIBLE_REQUIRED));
        }

        if (valorCausa is < 0)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.VALIDATION_OUT_OF_RANGE, nameof(ValorCausa)));
        }

        ClienteId = clienteId;
        ResponsavelUserId = responsavelUserId;
        Area = area;
        Foro = foro ?? DadosForo.Vazio;
        Assunto = Normalizar(assunto);
        ParteContraria = Normalizar(parteContraria);
        ValorCausa = valorCausa;
        DataDistribuicao = dataDistribuicao;
        Observacoes = Normalizar(observacoes);

        RaiseDomainEvent(new ProcessoAtualizadoEvent(Id.Value, TenantId));

        return Result.Success();
    }

    public Result ChangeStatus(StatusProcesso novoStatus)
    {
        if (novoStatus == Status)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PROCESS_INVALID_STATUS_TRANSITION, Status, novoStatus));
        }

        if (Status == StatusProcesso.Arquivado)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PROCESS_ALREADY_ARCHIVED));
        }

        var statusAnterior = Status;
        Status = novoStatus;

        RaiseDomainEvent(new ProcessoStatusAlteradoEvent(Id.Value, TenantId, statusAnterior, novoStatus));

        return Result.Success();
    }

    public Result Archive() => ChangeStatus(StatusProcesso.Arquivado);

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
