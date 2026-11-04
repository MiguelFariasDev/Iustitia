using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Errors.Exceptions;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Modules.Integrations.CNJ.Application.Abstractions;
using Advocacia.Modules.Integrations.CNJ.Infrastructure;
using Advocacia.Modules.Integrations.CNJ.Infrastructure.Djen;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.CapturePublication;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Infrastructure.Jobs;
using Advocacia.Modules.Legal.Infrastructure.Observability;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Legal.IntegrationTests.Common;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Legal.IntegrationTests.Jobs;

/// <summary>
/// Captura de ponta a ponta: DJEN de mentira -> DjenClient REAL -> CapturePublicationHandler
/// REAL -> Postgres real (Testcontainers). O único dublê é o transporte HTTP, para não
/// depender da disponibilidade do CNJ nem de rede (ver FakeDjenServer).
/// </summary>
[Collection(LegalPostgresCollection.Name)]
public class CnjCaptureJobTests(LegalPostgresFixture fixture)
{
    private static readonly DateOnly Today = new(2026, 9, 28);

    [Fact]
    public async Task ExecuteAsync_PersistsEveryPublicationReturnedByTheCnj()
    {
        var tenantId = Guid.NewGuid();
        var djen = new FakeDjenServer().WithItems(
            new FakeDjenItem(1001, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-1)),
            new FakeDjenItem(1002, TestPublications.CnjNumberB, "TJSP", Today.AddDays(-1)));

        await using var harness = CreateHarness(djen, tenantId);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        var persisted = await harness.QueryPublicationsAsync(tenantId);

        persisted.Should().HaveCount(2);
        persisted.Select(publication => publication.ExternalId).Should().BeEquivalentTo(["1001", "1002"]);
        persisted.Should().OnlyContain(publication => publication.Status == PublicationStatus.PendingReview);
        persisted.Should().OnlyContain(publication => publication.Source == PublicationSource.DJEN);
        persisted.Should().OnlyContain(publication => publication.ProcessId == null);
    }

    [Fact]
    public async Task ExecuteAsync_NormalizesTheContentAndUnwrapsTheCnjNumber()
    {
        var tenantId = Guid.NewGuid();
        var djen = new FakeDjenServer().WithItems(
            new FakeDjenItem(2001, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-2),
                Text: "<p>Intimação   para\n manifestação&nbsp;imediata.</p>"));

        await using var harness = CreateHarness(djen, tenantId);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        var publication = (await harness.QueryPublicationsAsync(tenantId)).Single();

        publication.RawContent.Value.Should().Be("Intimação para manifestação imediata.");
        publication.CnjNumber.Value.Should().Be(TestPublications.CnjNumberA);
        publication.PublishedAt.ToDateOnly().Should().Be(Today.AddDays(-2));
    }

    [Fact]
    public async Task ExecuteAsync_IsIdempotent_ASecondRunPersistsNothingNew()
    {
        // A janela de busca do job é deliberadamente sobreposta entre execuções, então este é
        // o caminho mais percorrido em produção (ver ADR-056).
        var tenantId = Guid.NewGuid();
        var items = new[]
        {
            new FakeDjenItem(3001, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-1)),
            new FakeDjenItem(3002, TestPublications.CnjNumberB, "TJSP", Today.AddDays(-1)),
        };

        await using (var firstRun = CreateHarness(new FakeDjenServer().WithItems(items), tenantId))
        {
            await firstRun.Job.ExecuteAsync(CancellationToken.None);
        }

        await using var secondRun = CreateHarness(new FakeDjenServer().WithItems(items), tenantId);
        await secondRun.Job.ExecuteAsync(CancellationToken.None);

        (await secondRun.QueryPublicationsAsync(tenantId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_WalksEveryPage()
    {
        var tenantId = Guid.NewGuid();
        var items = Enumerable.Range(1, 5)
            .Select(index => new FakeDjenItem(4000 + index, BuildCnjNumber(index), "TJSP", Today.AddDays(-1)))
            .ToArray();

        var djen = new FakeDjenServer().WithItems(items);

        await using var harness = CreateHarness(djen, tenantId, pageSize: 2);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        (await harness.QueryPublicationsAsync(tenantId)).Should().HaveCount(5);
        djen.RequestedPages.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ExecuteAsync_StopsAtMaxPages_AndLeavesTheRestForTheNextRun()
    {
        var tenantId = Guid.NewGuid();
        var items = Enumerable.Range(1, 6)
            .Select(index => new FakeDjenItem(5000 + index, BuildCnjNumber(index), "TJSP", Today.AddDays(-1)))
            .ToArray();

        var djen = new FakeDjenServer().WithItems(items);

        await using var harness = CreateHarness(djen, tenantId, pageSize: 2, maxPages: 2);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        (await harness.QueryPublicationsAsync(tenantId)).Should().HaveCount(4);
        djen.RequestedPages.Should().Equal(1, 2);
    }

    [Fact]
    public async Task ExecuteAsync_QueriesEveryConfiguredTribunal()
    {
        var tenantId = Guid.NewGuid();
        var djen = new FakeDjenServer().WithItems(
            new FakeDjenItem(6001, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-1)),
            new FakeDjenItem(6002, TestPublications.CnjNumberB, "TJRJ", Today.AddDays(-1)));

        await using var harness = CreateHarness(djen, tenantId, tribunais: ["TJSP", "TJRJ"]);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        (await harness.QueryPublicationsAsync(tenantId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_CapturesForEveryConfiguredTenant_KeepingThemIsolated()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var djen = new FakeDjenServer().WithItems(
            new FakeDjenItem(7001, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-1)));

        await using var harness = CreateHarness(djen, tenantA, tenantB);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        // A mesma comunicação vale para os dois escritórios, cada um com a sua linha.
        (await harness.QueryPublicationsAsync(tenantA)).Should().ContainSingle();
        (await harness.QueryPublicationsAsync(tenantB)).Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteAsync_SkipsAnInvalidPublicationAndKeepsTheValidOnes()
    {
        // Resiliência por publicação: um número CNJ corrompido é dado ruim de UMA comunicação e
        // não pode custar as outras da página.
        var tenantId = Guid.NewGuid();
        var djen = new FakeDjenServer().WithItems(
            new FakeDjenItem(8001, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-1)),
            new FakeDjenItem(8002, "1234567-49.2026.8.26.0001", "TJSP", Today.AddDays(-1)),
            new FakeDjenItem(8003, TestPublications.CnjNumberB, "TJSP", Today.AddDays(-1)));

        await using var harness = CreateHarness(djen, tenantId);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        var persisted = await harness.QueryPublicationsAsync(tenantId);

        persisted.Select(publication => publication.ExternalId).Should().BeEquivalentTo(["8001", "8003"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheCnjIsDown_ThrowsSoHangfireRetries()
    {
        var tenantId = Guid.NewGuid();
        var djen = new FakeDjenServer()
            .WithItems(new FakeDjenItem(9001, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-1)))
            .FailingFromRequest(1);

        await using var harness = CreateHarness(djen, tenantId);

        var act = () => harness.Job.ExecuteAsync(CancellationToken.None);

        var exception = await act.Should().ThrowAsync<IntegrationException>();
        exception.Which.Code.Should().Be(ErrorCode.CNJ_UNAVAILABLE);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheCnjDiesMidRun_KeepsWhatWasAlreadyPersisted()
    {
        // Falha parcial: o que entrou no banco permanece (cada publicação é sua própria
        // transação) e a retentativa do Hangfire vê duplicata e segue do ponto certo.
        var tenantId = Guid.NewGuid();
        var items = Enumerable.Range(1, 4)
            .Select(index => new FakeDjenItem(9100 + index, BuildCnjNumber(index), "TJSP", Today.AddDays(-1)))
            .ToArray();

        var djen = new FakeDjenServer().WithItems(items).FailingFromRequest(2);

        await using var harness = CreateHarness(djen, tenantId, pageSize: 2);

        var act = () => harness.Job.ExecuteAsync(CancellationToken.None);
        await act.Should().ThrowAsync<IntegrationException>();

        (await harness.QueryPublicationsAsync(tenantId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneTribunalFails_StillCapturesTheOthers_ThenThrows()
    {
        var tenantId = Guid.NewGuid();
        var djen = new FakeDjenServer()
            .WithItems(
                new FakeDjenItem(9201, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-1)),
                new FakeDjenItem(9202, TestPublications.CnjNumberB, "TJRJ", Today.AddDays(-1)))
            // Primeira requisição (TJSP) passa; a segunda (TJRJ) cai.
            .FailingFromRequest(2);

        await using var harness = CreateHarness(djen, tenantId, tribunais: ["TJSP", "TJRJ"]);

        var act = () => harness.Job.ExecuteAsync(CancellationToken.None);
        await act.Should().ThrowAsync<IntegrationException>();

        (await harness.QueryPublicationsAsync(tenantId))
            .Select(publication => publication.ExternalId).Should().Equal("9201");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_DoesNotCallTheCnj()
    {
        var tenantId = Guid.NewGuid();
        var djen = new FakeDjenServer().WithItems(
            new FakeDjenItem(9301, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-1)));

        await using var harness = CreateHarness(djen, tenantId, enabled: false);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        djen.RequestCount.Should().Be(0);
        (await harness.QueryPublicationsAsync(tenantId)).Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutConfiguredTenants_DoesNotCallTheCnj()
    {
        var djen = new FakeDjenServer().WithItems(
            new FakeDjenItem(9401, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-1)));

        await using var harness = CreateHarness(djen);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        djen.RequestCount.Should().Be(0);
    }


    [Fact]
    public async Task ExecuteAsync_ByProcess_CapturesOnlyThatProcessesPublications()
    {
        // Modo dirigido (Etapa 1.3): o escritório já sabe quais processos acompanha.
        var tenantId = Guid.NewGuid();
        var djen = new FakeDjenServer().WithItems(
            new FakeDjenItem(10001, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-30)),
            new FakeDjenItem(10002, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-2)),
            new FakeDjenItem(10003, TestPublications.CnjNumberB, "TJRJ", Today.AddDays(-1)));

        await using var harness = CreateHarness(djen, tenantId, cnjNumbers: [TestPublications.CnjNumberA]);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        var persisted = await harness.QueryPublicationsAsync(tenantId);

        persisted.Select(publication => publication.ExternalId).Should().BeEquivalentTo(["10001", "10002"]);
        persisted.Should().OnlyContain(publication => publication.CnjNumber.Value == TestPublications.CnjNumberA);
    }

    [Fact]
    public async Task ExecuteAsync_ByProcess_SendsTheUnmaskedNumberToTheCnj()
    {
        var tenantId = Guid.NewGuid();
        var djen = new FakeDjenServer().WithItems(
            new FakeDjenItem(10101, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-1)));

        await using var harness = CreateHarness(djen, tenantId, cnjNumbers: [TestPublications.CnjNumberA]);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        djen.RequestedProcesses.Should().OnlyContain(numero => numero == "12345674820268260001");
    }

    [Fact]
    public async Task ExecuteAsync_ByProcess_IgnoresTheDateWindowByDefault()
    {
        // Um processo recém-cadastrado precisa do histórico ANTERIOR ao cadastro: publicação
        // de 2 anos atrás tem de vir, mesmo com LookbackDays = 3.
        var tenantId = Guid.NewGuid();
        var djen = new FakeDjenServer().WithItems(
            new FakeDjenItem(10201, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-730)));

        await using var harness = CreateHarness(djen, tenantId, cnjNumbers: [TestPublications.CnjNumberA]);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        (await harness.QueryPublicationsAsync(tenantId)).Should().ContainSingle()
            .Which.ExternalId.Should().Be("10201");
    }

    [Fact]
    public async Task ExecuteAsync_CombinesTribunalSweepAndProcessCapture()
    {
        var tenantId = Guid.NewGuid();
        var djen = new FakeDjenServer().WithItems(
            new FakeDjenItem(10301, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-1)),
            new FakeDjenItem(10302, TestPublications.CnjNumberB, "TJRJ", Today.AddDays(-1)));

        await using var harness = CreateHarness(
            djen, tenantId, tribunais: ["TJSP"], cnjNumbers: [TestPublications.CnjNumberB]);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        // Um veio pela varredura do diário, o outro pela consulta dirigida.
        (await harness.QueryPublicationsAsync(tenantId))
            .Select(publication => publication.ExternalId).Should().BeEquivalentTo(["10301", "10302"]);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessAlsoFoundByTheTribunalSweep_IsDeduplicated()
    {
        // Os dois modos podem alcançar a MESMA comunicação — a deduplicação por ExternalId é
        // o que impede a publicação de entrar duas vezes (ver ADR-056).
        var tenantId = Guid.NewGuid();
        var djen = new FakeDjenServer().WithItems(
            new FakeDjenItem(10401, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-1)));

        await using var harness = CreateHarness(
            djen, tenantId, tribunais: ["TJSP"], cnjNumbers: [TestPublications.CnjNumberA]);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        (await harness.QueryPublicationsAsync(tenantId)).Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTribunaisAndWithoutProcesses_DoesNotCallTheCnj()
    {
        // Lista de tribunais vazia NÃO significa "todos": sem filtro o DJEN devolveria o
        // diário nacional inteiro.
        var tenantId = Guid.NewGuid();
        var djen = new FakeDjenServer().WithItems(
            new FakeDjenItem(10501, TestPublications.CnjNumberA, "TJSP", Today.AddDays(-1)));

        await using var harness = CreateHarness(djen, tenantId, tribunais: [], cnjNumbers: []);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        djen.RequestCount.Should().Be(0);
        (await harness.QueryPublicationsAsync(tenantId)).Should().BeEmpty();
    }


    [Fact]
    public async Task ExecuteAsync_WhenTheCnjLiesAboutTheTotalCount_StillWalksEveryPage()
    {
        // Defeito REAL do DJEN, medido na Etapa 1.3: com itensPorPagina=100 a API devolve
        // count=100 (o tamanho da página) em vez do total. A versão anterior de HasMorePages
        // confiava nesse valor, parava na página 1 e perdia o resto — sem erro, sem log e sem
        // métrica. Ver docs/poc/cnj-poc-report.md.
        var tenantId = Guid.NewGuid();
        var items = Enumerable.Range(1, 7)
            .Select(index => new FakeDjenItem(20000 + index, BuildCnjNumber(index), "TJSP", Today.AddDays(-1)))
            .ToArray();

        var djen = new FakeDjenServer().WithItems(items).ReportingPageSizeAsTotalCount();

        await using var harness = CreateHarness(djen, tenantId, pageSize: 3);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        (await harness.QueryPublicationsAsync(tenantId)).Should().HaveCount(7);
        djen.RequestedPages.Should().Equal(1, 2, 3);
    }

    /// <summary>Número CNJ válido (dígitos verificadores corretos) e distinto por índice.</summary>
    private static string BuildCnjNumber(int index)
    {
        var sequential = 1000000 + index;
        var withoutCheckDigits = $"{sequential:D7}2026826{0:D4}";
        var remainder = (int)(System.Numerics.BigInteger.Parse(withoutCheckDigits + "00") % 97);
        var checkDigits = 98 - remainder;

        return $"{sequential:D7}-{checkDigits:D2}.2026.8.26.0000";
    }

    private CaptureHarness CreateHarness(
        FakeDjenServer djen,
        params Guid[] tenantIds) =>
        CreateHarness(djen, tenantIds, null, null, null, null, true);

    private CaptureHarness CreateHarness(
        FakeDjenServer djen,
        Guid tenantId,
        int? pageSize = null,
        int? maxPages = null,
        IReadOnlyList<string>? tribunais = null,
        IReadOnlyList<string>? cnjNumbers = null,
        bool enabled = true) =>
        CreateHarness(djen, [tenantId], pageSize, maxPages, tribunais, cnjNumbers, enabled);

    private CaptureHarness CreateHarness(
        FakeDjenServer djen,
        Guid[] tenantIds,
        int? pageSize,
        int? maxPages,
        IReadOnlyList<string>? tribunais,
        IReadOnlyList<string>? cnjNumbers,
        bool enabled)
    {
        var cnjOptions = new CnjOptions { BaseUrl = "https://djen.invalid/", MaxPageSize = 100 };
        var httpClient = new HttpClient(djen) { BaseAddress = new Uri(cnjOptions.BaseUrl) };
        ICnjClient cnjClient = new DjenClient(
            httpClient, Options.Create(cnjOptions), NullLogger<DjenClient>.Instance);

        var tenantContext = new AsyncLocalTenantContext();
        var dbContext = TestLegalDbContextFactory.Create(fixture.SuperuserConnectionString, tenantContext);
        var repository = new PublicationRepository(dbContext);
        var processoRepository = new ProcessoRepository(dbContext);
        var unitOfWork = new LegalUnitOfWork(dbContext);
        IDateTimeProvider clock = new FixedClock(Today);

        var handler = new CapturePublicationHandler(
            repository, unitOfWork, clock, NullLogger<CapturePublicationHandler>.Instance);

        var jobOptions = new CnjCaptureJobOptions
        {
            Enabled = enabled,
            LookbackDays = 3,
            PageSize = pageSize ?? 100,
            MaxPages = maxPages ?? 50,
            Tribunais = tribunais ?? (cnjNumbers is null ? ["TJSP"] : []),
            CnjNumbers = cnjNumbers ?? [],
            TenantIds = tenantIds,
        };

        var job = new CnjCaptureJob(
            cnjClient,
            new DirectSender(handler),
            repository,
            processoRepository,
            tenantContext,
            clock,
            new CnjCaptureTelemetry(),
            Options.Create(jobOptions),
            NullLogger<CnjCaptureJob>.Instance);

        return new CaptureHarness(job, dbContext, httpClient);
    }

    private sealed class CaptureHarness(CnjCaptureJob job, LegalDbContext dbContext, HttpClient httpClient)
        : IAsyncDisposable
    {
        internal CnjCaptureJob Job { get; } = job;

        internal async Task<List<Publication>> QueryPublicationsAsync(Guid tenantId)
        {
            dbContext.ChangeTracker.Clear();

            return await dbContext.Publications
                .IgnoreQueryFilters()
                .Where(publication => publication.TenantId == tenantId && !publication.IsDeleted)
                .ToListAsync();
        }

        public async ValueTask DisposeAsync()
        {
            httpClient.Dispose();
            await dbContext.DisposeAsync();
        }
    }

    /// <summary>
    /// ISender mínimo que encaminha direto ao handler real, sem montar o pipeline do MediatR.
    /// Aqui se quer exercitar a lógica do JOB (paginação, deduplicação, tratamento de erro)
    /// sobre a persistência real — os behaviors (validação/transação/log) têm seus próprios
    /// testes em BuildingBlocks.Application.UnitTests.
    /// </summary>
    private sealed class DirectSender(CapturePublicationHandler handler) : ISender
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is CapturePublicationCommand command)
            {
                return (TResponse)(object)await handler.Handle(command, cancellationToken);
            }

            throw new NotSupportedException($"DirectSender não trata {request.GetType().Name}.");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedClock(DateOnly today) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; } = new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

        public DateOnly Today { get; } = today;
    }
}
