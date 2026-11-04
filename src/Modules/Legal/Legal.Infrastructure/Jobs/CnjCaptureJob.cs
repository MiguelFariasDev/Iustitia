using System.Diagnostics;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Errors.Exceptions;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Integrations.CNJ.Application.Abstractions;
using Advocacia.Modules.Integrations.CNJ.Application.Models;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.Alerts;
using Advocacia.Modules.Legal.Application.Features.CapturePublication;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Infrastructure.Observability;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Legal.Infrastructure.Jobs;

/// <summary>
/// Job recorrente que consulta o DJEN/CNJ e persiste as publicações novas (ver ADR-017 para
/// Hangfire e docs/modules/legal/cnj-capture.md para o fluxo completo).
///
/// A captura é DIRIGIDA PELO CADASTRO: para cada escritório, o job consulta o diário pelos
/// números dos processos que aquele escritório cadastrou e mantém ativos ou suspensos. Nenhuma
/// publicação entra na base de um escritório sem um processo dele que a justifique — a base
/// não é abastecida com o diário inteiro de um tribunal.
///
/// Garantias:
///   - Idempotente: reexecutar sobre o mesmo período não duplica nada (deduplicação por
///     ExternalId — ADR-056), então a janela de busca é deliberadamente sobreposta.
///   - Resiliente por publicação: uma comunicação com dado inválido é contada e logada, sem
///     abortar a página nem o tribunal. Só falha de integração/persistência relança.
///   - Tolerante a falha parcial: o resultado de cada tribunal é independente; se o CNJ cair
///     no meio, o que já foi persistido permanece e o job relança ao final para o Hangfire
///     retentar (o que já existe será visto como duplicata na retentativa).
///
/// NUNCA loga o conteúdo da publicação — só contadores, tribunal, página e ExternalId
/// (ver claude.md, seção 14).
/// </summary>
public sealed class CnjCaptureJob(
    ICnjClient cnjClient,
    ISender sender,
    IPublicationRepository publicationRepository,
    IProcessoRepository processoRepository,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    CnjCaptureTelemetry telemetry,
    IOptions<CnjCaptureJobOptions> options,
    ILogger<CnjCaptureJob> logger)
{
    private const PublicationSource Source = PublicationSource.DJEN;

    /// <summary>
    /// AutomaticRetry do Hangfire com backoff crescente: o CNJ costuma voltar em minutos, e
    /// uma nova tentativa é sempre segura por a captura ser idempotente.
    /// </summary>
    /// <remarks>
    /// DisableConcurrentExecution usa um lock distribuído no storage do Hangfire (PostgreSQL),
    /// então vale entre INSTÂNCIAS, não só entre threads do mesmo processo — necessário porque
    /// o Worker escala horizontalmente no Azure Container Apps.
    ///
    /// Sem ele, duas instâncias capturando ao mesmo tempo (a) desperdiçam o orçamento de rate
    /// limit do CNJ, que é por IP e não por processo, e (b) colidem no índice único de
    /// deduplicação — observado na prova de conceito (Etapa 1.3): duas execuções simultâneas
    /// produziram "23505 duplicate key value violates ux_publications_tenant_external_id". O
    /// índice protegeu o dado, mas a execução terminou em falha desnecessária.
    ///
    /// O timeout é bem maior que a duração esperada de uma execução: uma instância que morra
    /// no meio libera o lock ao expirar, em vez de travar a captura para sempre.
    /// </remarks>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 900])]
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var jobOptions = options.Value;

        if (!jobOptions.Enabled)
        {
            logger.LogInformation("CnjCaptureJob: desabilitado por configuração (Jobs:CnjCapture:Enabled=false).");
            return;
        }

        // Lista vazia (o normal) = todos os escritórios com processo ativo. A configuração
        // só restringe, nunca é a fonte de verdade de quem deve ser capturado.
        var tenantIds = jobOptions.TenantIds.Count > 0
            ? jobOptions.TenantIds
            : await processoRepository.ListTenantsComProcessosAtivosAsync(cancellationToken);

        if (tenantIds.Count == 0)
        {
            logger.LogInformation(
                "CnjCaptureJob: nenhum escritório com processo ativo cadastrado — nada a capturar.");
            return;
        }

        using var jobSpan = CnjCaptureTelemetry.StartJobSpan();
        var stopwatch = Stopwatch.StartNew();

        var (startDate, endDate) = ResolveDateRange(jobOptions);

        jobSpan?.SetTag("cnj.start_date", startDate.ToString("O"));
        jobSpan?.SetTag("cnj.end_date", endDate.ToString("O"));
        jobSpan?.SetTag("cnj.tenant_count", tenantIds.Count);

        logger.LogInformation(
            "CnjCaptureJob iniciado: {TenantCount} escritórios com processos ativos.", tenantIds.Count);

        var total = new CaptureTotals();
        Exception? firstFailure = null;

        foreach (var tenantId in tenantIds)
        {
            // O job roda fora de uma requisição HTTP: sem isso, o filtro global do
            // LegalDbContext e o RLS do Postgres não têm tenant para aplicar e as consultas
            // de deduplicação voltariam vazias (fail closed), fazendo o job recriar tudo.
            tenantContext.SetTenant(tenantId);

            // Os alvos são resolvidos POR ESCRITÓRIO, a partir dos processos dele — por isso
            // dentro do laço, depois de SetTenant: é o tenant da sessão que decide quais
            // processos a consulta enxerga (filtro global + RLS).
            var targets = await ResolveTargetsAsync(tenantId, jobOptions, startDate, endDate, cancellationToken);

            if (targets.Count == 0)
            {
                logger.LogInformation(
                    "CnjCaptureJob: escritório {TenantId} não tem processo ativo — nada a capturar.", tenantId);
                continue;
            }

            logger.LogInformation(
                "CnjCaptureJob: escritório {TenantId} com {TargetCount} alvos de captura.", tenantId, targets.Count);

            var processed = 0;

            foreach (var target in targets)
            {
                // Pausa entre lotes para espaçar as chamadas ao CNJ (rate limit por IP).
                if (processed > 0 && jobOptions.BatchSize > 0 && processed % jobOptions.BatchSize == 0
                    && jobOptions.DelayBetweenBatchesMs > 0)
                {
                    await Task.Delay(jobOptions.DelayBetweenBatchesMs, cancellationToken);
                }

                processed++;

                using var processSpan = CnjCaptureTelemetry.StartProcessSpan(target.Describe());

                try
                {
                    total.Add(await CaptureTargetAsync(tenantId, target, jobOptions, cancellationToken));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // Uma falha em um alvo não deve impedir os demais de serem capturados;
                    // a primeira exceção é guardada e relançada ao final para o Hangfire
                    // registrar a execução como falha e aplicar retry.
                    total.Errors++;
                    processSpan?.SetStatus(ActivityStatusCode.Error, exception.Message);
                    telemetry.RecordErrors(1, target.Mode, target.Tribunal);
                    firstFailure ??= exception;

                    logger.LogError(
                        exception,
                        "CnjCaptureJob: falha ao capturar {Criterio} para o escritório {TenantId}.",
                        target.Describe(), tenantId);
                }
            }
        }

        stopwatch.Stop();
        telemetry.RecordDuration(stopwatch.Elapsed);

        jobSpan?.SetTag("cnj.captured", total.Captured);
        jobSpan?.SetTag("cnj.duplicates", total.Duplicates);
        jobSpan?.SetTag("cnj.invalid", total.Invalid);
        jobSpan?.SetTag("cnj.errors", total.Errors);

        logger.LogInformation(
            "CnjCaptureJob concluído em {ElapsedMs}ms: {Captured} novas, {Duplicates} duplicadas, {Invalid} inválidas, {Errors} erros.",
            stopwatch.ElapsedMilliseconds, total.Captured, total.Duplicates, total.Invalid, total.Errors);

        if (firstFailure is not null)
        {
            jobSpan?.SetStatus(ActivityStatusCode.Error, firstFailure.Message);

            // Relança para o Hangfire marcar a execução como falha e retentar. O que já foi
            // persistido permanece — a retentativa vê duplicata e segue.
            throw firstFailure;
        }
    }

    /// <summary>
    /// Monta os alvos de captura DESTE escritório.
    ///
    /// A fonte de verdade são os PROCESSOS CADASTRADOS e acompanhados (ativos ou suspensos) —
    /// é isso que garante que a base do escritório receba só o que é dele. Um processo
    /// encerrado ou arquivado deixa de ser consultado (ver Processo.GeraCaptura).
    ///
    /// Os dois extras são ferramentas de diagnóstico, vazios em produção:
    ///   - <c>Tribunais</c>: varredura do diário inteiro, que traz processos de terceiros;
    ///   - <c>CnjNumbers</c>: números avulsos, para investigar sem cadastrar.
    ///
    /// Números repetidos entre as fontes são deduplicados aqui — consultar o mesmo processo
    /// duas vezes gastaria orçamento de rate limit do CNJ sem trazer nada novo.
    /// </summary>
    private async Task<List<CaptureTarget>> ResolveTargetsAsync(
        Guid tenantId,
        CnjCaptureJobOptions jobOptions,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        // Sem janela de datas por padrão: um processo recém-cadastrado precisa das
        // publicações ANTERIORES ao cadastro, não só das da última janela.
        var janelaInicio = jobOptions.RestrictProcessCaptureToWindow ? (DateOnly?)startDate : null;
        var janelaFim = jobOptions.RestrictProcessCaptureToWindow ? (DateOnly?)endDate : null;

        var processos = await processoRepository.ListParaCapturaAsync(tenantId, cancellationToken);

        var numeros = new HashSet<string>(StringComparer.Ordinal);
        var targets = new List<CaptureTarget>();

        foreach (var processo in processos)
        {
            if (numeros.Add(processo.CnjDigits))
            {
                targets.Add(CaptureTarget.ForProcess(
                    processo.CnjDigits, janelaInicio, janelaFim, processo.Id.Value, processo.ClienteId, processo.ResponsavelUserId));
            }
        }

        foreach (var avulso in jobOptions.CnjNumbers.Where(numero => !string.IsNullOrWhiteSpace(numero)))
        {
            var digitos = new string(avulso.Where(char.IsDigit).ToArray());

            if (numeros.Add(digitos))
            {
                targets.Add(CaptureTarget.ForProcess(avulso, janelaInicio, janelaFim));
            }
        }

        targets.AddRange(jobOptions.Tribunais
            .Where(tribunal => !string.IsNullOrWhiteSpace(tribunal))
            .Select(tribunal => CaptureTarget.ForTribunal(tribunal, startDate, endDate)));

        return targets;
    }

    private async Task<CaptureTotals> CaptureTargetAsync(
        Guid tenantId,
        CaptureTarget target,
        CnjCaptureJobOptions jobOptions,
        CancellationToken cancellationToken)
    {
        var totals = new CaptureTotals();

        for (var page = 1; page <= jobOptions.MaxPages; page++)
        {
            using var pageSpan = CnjCaptureTelemetry.StartPageSpan(target.Describe(), page);

            var query = target.ToQuery(page, jobOptions.PageSize);
            var pageResult = await cnjClient.GetPublicationsAsync(query, cancellationToken);

            if (pageResult.IsFailure)
            {
                pageSpan?.SetStatus(ActivityStatusCode.Error, pageResult.Error.Code);
                telemetry.RecordErrors(1, target.Mode, target.Tribunal);

                // Relança como exception: é falha de integração (não regra de negócio), e o
                // Hangfire precisa ver a execução falhar para aplicar retry (ver ADR-033).
                // Error.Code é sempre o nome de um ErrorCode do catálogo (ver ErrorDefinition),
                // então o parse só falharia com um Error construído fora do ErrorFactory.
                var errorCode = Enum.TryParse<ErrorCode>(pageResult.Error.Code, out var parsed)
                    ? parsed
                    : ErrorCode.CNJ_UNAVAILABLE;

                throw new IntegrationException(errorCode, pageResult.Error.Message);
            }

            var cnjPage = pageResult.Value;

            if (cnjPage.Items.Count == 0)
            {
                break;
            }

            var pageTotals = await PersistPageAsync(tenantId, target, cnjPage, cancellationToken);
            totals.Add(pageTotals);

            pageSpan?.SetTag("cnj.page.items", cnjPage.Items.Count);
            pageSpan?.SetTag("cnj.page.captured", pageTotals.Captured);
            pageSpan?.SetTag("cnj.page.duplicates", pageTotals.Duplicates);

            if (!cnjPage.HasMorePages)
            {
                break;
            }

            if (page == jobOptions.MaxPages)
            {
                logger.LogWarning(
                    "CnjCaptureJob: teto de {MaxPages} páginas atingido para {Criterio} — há publicações não capturadas. Reduza a janela (LookbackDays) ou aumente MaxPages.",
                    jobOptions.MaxPages, target.Describe());
            }
        }

        return totals;
    }

    private async Task<CaptureTotals> PersistPageAsync(
        Guid tenantId, CaptureTarget target, CnjPublicationPage cnjPage, CancellationToken cancellationToken)
    {
        var totals = new CaptureTotals();

        // Pré-filtro em uma única consulta: numa reexecução (o caso comum, pela janela
        // sobreposta) a maioria da página já existe e é descartada sem nenhum round-trip
        // individual ao banco.
        var externalIds = cnjPage.Items.Select(item => item.ExternalId).ToList();
        var alreadyPersisted = await publicationRepository.FindExistingExternalIdsAsync(
            tenantId, externalIds, Source, cancellationToken);

        foreach (var item in cnjPage.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (alreadyPersisted.Contains(item.ExternalId))
            {
                totals.Duplicates++;
                continue;
            }

            using var publicationSpan = CnjCaptureTelemetry.StartPublicationSpan(item.ExternalId);

            var command = new CapturePublicationCommand(
                TenantId: tenantId,
                ExternalId: item.ExternalId,
                CnjNumber: item.CnjNumber,
                Source: Source,
                RawContent: item.RawContent,
                PublishedAt: item.PublishedAt,
                Type: MapType(item.CommunicationType),
                MetadataJson: item.MetadataJson,
                ProcessId: target.ProcessoId,
                ClienteId: target.ClienteId,
                ResponsavelUserId: target.ResponsavelUserId);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                // Erro de negócio (CNJ mal formado, data no futuro, conteúdo vazio) é dado
                // ruim de UMA publicação: conta, loga e segue. Nunca aborta a página.
                totals.Invalid++;
                publicationSpan?.SetStatus(ActivityStatusCode.Error, result.Error.Code);

                logger.LogWarning(
                    "CnjCaptureJob: publicação {ExternalId} ({Criterio}) rejeitada ({ErrorCode}): {ErrorMessage}",
                    item.ExternalId, target.Describe(), result.Error.Code, result.Error.Message);

                continue;
            }

            if (result.Value.Existing)
            {
                totals.Duplicates++;
            }
            else
            {
                totals.Captured++;
                await CreateAlertAsync(result.Value.PublicationId, target, cancellationToken);
            }
        }

        telemetry.RecordCaptured(totals.Captured, target.Mode, target.Tribunal);
        telemetry.RecordDuplicates(totals.Duplicates, target.Mode, target.Tribunal);
        telemetry.RecordInvalid(totals.Invalid, target.Mode, target.Tribunal);

        return totals;
    }

    /// <summary>
    /// Tipo da publicação a partir do "tipoComunicacao" do DJEN (ex.: "Intimação"). É o que a
    /// ORIGEM informa — não é classificação por IA (Fase 3, sempre com revisão humana). Valor
    /// desconhecido fica nulo: a tela mostra "Outros" em vez de um palpite.
    /// </summary>
    private static PublicationType? MapType(string? communicationType) =>
        communicationType?.Trim().ToLowerInvariant() switch
        {
            { } value when value.StartsWith("intima", StringComparison.Ordinal) => PublicationType.Intimacao,
            { } value when value.StartsWith("senten", StringComparison.Ordinal) => PublicationType.Sentenca,
            { } value when value.StartsWith("despacho", StringComparison.Ordinal) => PublicationType.Despacho,
            { } value when value.StartsWith("decis", StringComparison.Ordinal) => PublicationType.Decisao,
            { } value when value.StartsWith("ac", StringComparison.Ordinal) && value.Contains("rd", StringComparison.Ordinal) => PublicationType.Acordao,
            _ => null,
        };

    /// <summary>
    /// Alerta in-app para o responsável pelo processo. Best-effort: a publicação já está
    /// persistida e é o que importa, então uma falha aqui é logada e NÃO derruba a captura —
    /// o alerta é conveniência, a publicação continua visível na tela de publicações.
    /// </summary>
    private async Task CreateAlertAsync(Guid publicationId, CaptureTarget target, CancellationToken cancellationToken)
    {
        if (target.ResponsavelUserId is not { } userId || userId == Guid.Empty)
        {
            return;
        }

        try
        {
            var alert = await sender.Send(new CreateAlertFromPublicationCommand(publicationId, userId), cancellationToken);

            if (alert.IsFailure)
            {
                logger.LogWarning(
                    "CnjCaptureJob: alerta da publicação {PublicationId} não criado ({ErrorCode}).",
                    publicationId, alert.Error.Code);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception, "CnjCaptureJob: falha ao criar o alerta da publicação {PublicationId}.", publicationId);
        }
    }

    /// <summary>
    /// Datas fixas (StartDate/EndDate) têm precedência; sem elas, usa a janela relativa de
    /// <see cref="CnjCaptureJobOptions.LookbackDays"/> terminando hoje — o modo correto para
    /// execução recorrente, já que datas fixas em appsettings silenciosamente param de
    /// capturar quando o período passa.
    /// </summary>
    private (DateOnly StartDate, DateOnly EndDate) ResolveDateRange(CnjCaptureJobOptions jobOptions)
    {
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        var endDate = jobOptions.EndDate ?? today;
        var startDate = jobOptions.StartDate ?? endDate.AddDays(-Math.Max(jobOptions.LookbackDays, 0));

        return startDate > endDate ? (endDate, endDate) : (startDate, endDate);
    }

    /// <summary>
    /// Um alvo de captura: ou um tribunal no período, ou um processo acompanhado.
    /// <see cref="Tribunal"/> é nulo no modo processo — o tribunal só é conhecido depois de
    /// a publicação chegar, e por isso a tag de métrica fica "-" nesse modo.
    /// </summary>
    private sealed record CaptureTarget(
        CaptureMode Mode,
        string? Tribunal,
        string? CnjNumber,
        DateOnly? StartDate,
        DateOnly? EndDate,
        Guid? ProcessoId,
        Guid? ClienteId = null,
        Guid? ResponsavelUserId = null)
    {
        internal static CaptureTarget ForTribunal(string tribunal, DateOnly startDate, DateOnly endDate) =>
            new(CaptureMode.Tribunal, tribunal, CnjNumber: null, startDate, endDate, ProcessoId: null);

        /// <summary>
        /// <paramref name="processoId"/> é o processo cadastrado de onde o número veio; nulo
        /// para número avulso de diagnóstico, caso em que a publicação fica sem vínculo.
        /// </summary>
        internal static CaptureTarget ForProcess(
            string cnjNumber,
            DateOnly? startDate,
            DateOnly? endDate,
            Guid? processoId = null,
            Guid? clienteId = null,
            Guid? responsavelUserId = null) =>
            new(CaptureMode.Processo, Tribunal: null, cnjNumber, startDate, endDate, processoId, clienteId, responsavelUserId);

        internal CnjPublicationQuery ToQuery(int page, int pageSize) => Mode == CaptureMode.Processo
            ? CnjPublicationQuery.ByProcess(CnjNumber!, StartDate, EndDate, page, pageSize)
            : CnjPublicationQuery.ByPeriod(StartDate!.Value, EndDate!.Value, Tribunal, page, pageSize);

        internal string Describe() => Mode == CaptureMode.Processo
            ? $"processo={CnjNumber}"
            : $"tribunal={Tribunal}";
    }

    private sealed class CaptureTotals
    {
        public int Captured { get; set; }

        public int Duplicates { get; set; }

        public int Invalid { get; set; }

        public int Errors { get; set; }

        public void Add(CaptureTotals other)
        {
            Captured += other.Captured;
            Duplicates += other.Duplicates;
            Invalid += other.Invalid;
            Errors += other.Errors;
        }
    }
}
