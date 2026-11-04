using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Modules.Integrations.CNJ.Application.Abstractions;
using Advocacia.Modules.Integrations.CNJ.Infrastructure;
using Advocacia.Modules.Integrations.CNJ.Infrastructure.Djen;
using Advocacia.Modules.Legal.Application.Features.Alerts;
using Advocacia.Modules.Legal.Application.Features.CapturePublication;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Shared;
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
/// A garantia central do produto: o escritório recebe as publicações DOS PROCESSOS DELE, e
/// só delas. A base não é abastecida com o diário inteiro de um tribunal — um escritório que
/// cadastrou 3 processos não pode acordar com 14 mil publicações de terceiros.
///
/// O único dublê é o transporte HTTP (FakeDjenServer no formato real do DJEN); o cliente, os
/// handlers, os repositórios e o Postgres são os de produção.
/// </summary>
[Collection(LegalPostgresCollection.Name)]
public class CapturaDirigidaPorProcessoTests(LegalPostgresFixture fixture)
{
    private static readonly DateOnly Hoje = new(2026, 9, 29);

    private const string CnjDoEscritorio = "1234567-48.2026.8.26.0001";
    private const string CnjDeTerceiro = "7654321-87.2025.8.19.0500";

    [Fact]
    public async Task CapturaSomenteOsProcessosCadastrados_IgnorandoOsDeTerceiros()
    {
        var tenantId = Guid.NewGuid();
        await using var harness = CriarHarness(tenantId);

        await harness.CadastrarProcessoAsync(CnjDoEscritorio);

        // O DJEN tem publicações dos dois processos; só um é do escritório.
        harness.Djen
            .WithItems(
                new FakeDjenItem(5001, CnjDoEscritorio, "TJSP", Hoje.AddDays(-1)),
                new FakeDjenItem(5002, CnjDeTerceiro, "TJRJ", Hoje.AddDays(-1)));

        await harness.Job.ExecuteAsync(CancellationToken.None);

        var publicacoes = await harness.ListarPublicacoesAsync(tenantId);

        publicacoes.Should().ContainSingle("apenas o processo cadastrado deve ser consultado");
        publicacoes[0].CnjNumber.Value.Should().Be(CnjDoEscritorio);
    }

    [Fact]
    public async Task VinculaAPublicacaoAoProcessoJaNaCaptura()
    {
        var tenantId = Guid.NewGuid();
        await using var harness = CriarHarness(tenantId);

        var processoId = await harness.CadastrarProcessoAsync(CnjDoEscritorio);

        harness.Djen.WithItems(new FakeDjenItem(5101, CnjDoEscritorio, "TJSP", Hoje.AddDays(-1)));

        await harness.Job.ExecuteAsync(CancellationToken.None);

        var publicacao = (await harness.ListarPublicacoesAsync(tenantId)).Should().ContainSingle().Subject;

        // Sem o vínculo, a publicação não aparece na timeline de nenhum processo.
        publicacao.ProcessId.Should().Be(processoId);
    }

    [Fact]
    public async Task VinculaAPublicacaoAoClienteEAoResponsavelDoProcesso()
    {
        var tenantId = Guid.NewGuid();
        var responsavelId = Guid.NewGuid();
        await using var harness = CriarHarness(tenantId);

        var processoId = await harness.CadastrarProcessoAsync(CnjDoEscritorio, responsavelUserId: responsavelId);
        var clienteId = await harness.ClienteDoProcessoAsync(processoId);

        harness.Djen.WithItems(new FakeDjenItem(5111, CnjDoEscritorio, "TJSP", Hoje.AddDays(-1)));

        await harness.Job.ExecuteAsync(CancellationToken.None);

        var publicacao = (await harness.ListarPublicacoesAsync(tenantId)).Should().ContainSingle().Subject;
        publicacao.ClienteId.Should().Be(clienteId);
        publicacao.ResponsavelUserId.Should().Be(responsavelId);
        publicacao.Tribunal.Should().Be("TJSP");
    }

    [Fact]
    public async Task CriaUmAlertaParaOResponsavel_SemRepetirNaReexecucao()
    {
        var tenantId = Guid.NewGuid();
        var responsavelId = Guid.NewGuid();
        await using var harness = CriarHarness(tenantId);

        await harness.CadastrarProcessoAsync(CnjDoEscritorio, responsavelUserId: responsavelId);
        harness.Djen.WithItems(new FakeDjenItem(5121, CnjDoEscritorio, "TJSP", Hoje.AddDays(-1)));

        await harness.Job.ExecuteAsync(CancellationToken.None);
        await harness.Job.ExecuteAsync(CancellationToken.None);

        // A segunda execução só vê duplicata: nenhum alerta novo.
        harness.AlertasCriados.Should().ContainSingle().Which.UserId.Should().Be(responsavelId);
    }

    [Fact]
    public async Task FalhaNoAlerta_NaoDerrubaACaptura()
    {
        var tenantId = Guid.NewGuid();
        await using var harness = CriarHarness(tenantId);

        await harness.CadastrarProcessoAsync(CnjDoEscritorio);
        harness.AlertasFalham = true;
        harness.Djen.WithItems(new FakeDjenItem(5131, CnjDoEscritorio, "TJSP", Hoje.AddDays(-1)));

        var act = async () => await harness.Job.ExecuteAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        (await harness.ListarPublicacoesAsync(tenantId)).Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessoEncerrado_DeixaDeSerConsultado()
    {
        var tenantId = Guid.NewGuid();
        await using var harness = CriarHarness(tenantId);

        await harness.CadastrarProcessoAsync(CnjDoEscritorio, StatusProcesso.Encerrado);

        harness.Djen.WithItems(new FakeDjenItem(5201, CnjDoEscritorio, "TJSP", Hoje.AddDays(-1)));

        await harness.Job.ExecuteAsync(CancellationToken.None);

        harness.Djen.RequestCount.Should().Be(0, "processo encerrado não gera consulta ao CNJ");
        (await harness.ListarPublicacoesAsync(tenantId)).Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessoSuspenso_ContinuaSendoConsultado()
    {
        // Um processo sobrestado volta a publicar sem aviso — deixar de consultá-lo é
        // exatamente como se perde um prazo.
        var tenantId = Guid.NewGuid();
        await using var harness = CriarHarness(tenantId);

        await harness.CadastrarProcessoAsync(CnjDoEscritorio, StatusProcesso.Suspenso);

        harness.Djen.WithItems(new FakeDjenItem(5301, CnjDoEscritorio, "TJSP", Hoje.AddDays(-1)));

        await harness.Job.ExecuteAsync(CancellationToken.None);

        (await harness.ListarPublicacoesAsync(tenantId)).Should().ContainSingle();
    }

    [Fact]
    public async Task EscritorioSemProcessoCadastrado_NaoConsultaOCnj()
    {
        var tenantId = Guid.NewGuid();
        await using var harness = CriarHarness(tenantId);

        harness.Djen.WithItems(new FakeDjenItem(5401, CnjDoEscritorio, "TJSP", Hoje.AddDays(-1)));

        await harness.Job.ExecuteAsync(CancellationToken.None);

        harness.Djen.RequestCount.Should().Be(0);
        (await harness.ListarPublicacoesAsync(tenantId)).Should().BeEmpty();
    }

    [Fact]
    public async Task CapturaOHistoricoCompletoDoProcessoRecemCadastrado()
    {
        // Publicação de dois anos atrás precisa vir: o processo é novo no sistema, não no
        // Judiciário.
        var tenantId = Guid.NewGuid();
        await using var harness = CriarHarness(tenantId);

        await harness.CadastrarProcessoAsync(CnjDoEscritorio);

        harness.Djen.WithItems(new FakeDjenItem(5501, CnjDoEscritorio, "TJSP", Hoje.AddDays(-730)));

        await harness.Job.ExecuteAsync(CancellationToken.None);

        (await harness.ListarPublicacoesAsync(tenantId)).Should().ContainSingle();
    }

    [Fact]
    public async Task CadaEscritorioRecebeSomenteAsPublicacoesDosSeusProcessos()
    {
        var escritorioA = Guid.NewGuid();
        var escritorioB = Guid.NewGuid();

        await using var harnessA = CriarHarness(escritorioA);
        await harnessA.CadastrarProcessoAsync(CnjDoEscritorio);

        await using var harnessB = CriarHarness(escritorioB);
        await harnessB.CadastrarProcessoAsync(CnjDeTerceiro);

        var itens = new[]
        {
            new FakeDjenItem(5601, CnjDoEscritorio, "TJSP", Hoje.AddDays(-1)),
            new FakeDjenItem(5602, CnjDeTerceiro, "TJRJ", Hoje.AddDays(-1)),
        };

        harnessA.Djen.WithItems(itens);
        harnessB.Djen.WithItems(itens);

        await harnessA.Job.ExecuteAsync(CancellationToken.None);
        await harnessB.Job.ExecuteAsync(CancellationToken.None);

        (await harnessA.ListarPublicacoesAsync(escritorioA)).Should().ContainSingle()
            .Which.CnjNumber.Value.Should().Be(CnjDoEscritorio);

        (await harnessB.ListarPublicacoesAsync(escritorioB)).Should().ContainSingle()
            .Which.CnjNumber.Value.Should().Be(CnjDeTerceiro);
    }

    private CapturaHarness CriarHarness(Guid tenantId)
    {
        var djen = new FakeDjenServer();
        var cnjOptions = new CnjOptions { BaseUrl = "https://djen.invalid/" };
        var httpClient = new HttpClient(djen) { BaseAddress = new Uri(cnjOptions.BaseUrl) };
        ICnjClient cnjClient = new DjenClient(httpClient, Options.Create(cnjOptions), NullLogger<DjenClient>.Instance);

        var tenantContext = new AsyncLocalTenantContext();
        tenantContext.SetTenant(tenantId);

        var dbContext = TestLegalDbContextFactory.Create(fixture.SuperuserConnectionString, tenantContext);
        var publicationRepository = new PublicationRepository(dbContext);
        var processoRepository = new ProcessoRepository(dbContext);
        var unitOfWork = new LegalUnitOfWork(dbContext);
        IDateTimeProvider clock = new RelogioFixo(Hoje);

        var handler = new CapturePublicationHandler(
            publicationRepository, unitOfWork, clock, NullLogger<CapturePublicationHandler>.Instance);

        var alertas = new AlertPublisherGravador();
        var alertHandler = new CreateAlertFromPublicationHandler(publicationRepository, alertas);

        var job = new CnjCaptureJob(
            cnjClient,
            new SenderDireto(handler, alertHandler),
            publicationRepository,
            processoRepository,
            tenantContext,
            clock,
            new CnjCaptureTelemetry(),
            // TenantIds explícito para o teste não depender do estado global do banco.
            Options.Create(new CnjCaptureJobOptions { Enabled = true, TenantIds = [tenantId] }),
            NullLogger<CnjCaptureJob>.Instance);

        return new CapturaHarness(job, djen, dbContext, httpClient, tenantId, alertas);
    }

    private sealed class CapturaHarness(
        CnjCaptureJob job,
        FakeDjenServer djen,
        LegalDbContext dbContext,
        HttpClient httpClient,
        Guid tenantId,
        AlertPublisherGravador alertas)
        : IAsyncDisposable
    {
        internal IReadOnlyList<AlertRequest> AlertasCriados => alertas.Criados;

        internal bool AlertasFalham { set => alertas.Falhar = value; }

        internal async Task<Guid> ClienteDoProcessoAsync(Guid processoId)
        {
            dbContext.ChangeTracker.Clear();
            var processo = await dbContext.Processos.IgnoreQueryFilters().SingleAsync(p => p.Id == ProcessoId.From(processoId));

            return processo.ClienteId;
        }

        internal CnjCaptureJob Job { get; } = job;

        internal FakeDjenServer Djen { get; } = djen;

        /// <summary>Cadastra cliente e processo como a advogada faria pela tela.</summary>
        internal async Task<Guid> CadastrarProcessoAsync(
            string cnjNumber, StatusProcesso status = StatusProcesso.Ativo, Guid? responsavelUserId = null)
        {
            var cliente = Cliente.Create(
                tenantId,
                TipoCliente.PessoaFisica,
                NomeCliente.Create("Cliente de Teste").Value,
                Documento.Create("11144477735", TipoCliente.PessoaFisica).Value).Value;

            dbContext.Clientes.Add(cliente);

            var processo = Processo.Create(
                tenantId, CNJNumber.Create(cnjNumber).Value, cliente.Id.Value, responsavelUserId ?? Guid.NewGuid(), AreaDireito.Civel).Value;

            if (status != StatusProcesso.Ativo)
            {
                processo.ChangeStatus(status);
            }

            dbContext.Processos.Add(processo);
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();

            return processo.Id.Value;
        }

        internal async Task<List<Publication>> ListarPublicacoesAsync(Guid tenant)
        {
            dbContext.ChangeTracker.Clear();

            return await dbContext.Publications
                .IgnoreQueryFilters()
                .Where(publicacao => publicacao.TenantId == tenant && !publicacao.IsDeleted)
                .ToListAsync();
        }

        public async ValueTask DisposeAsync()
        {
            httpClient.Dispose();
            await dbContext.DisposeAsync();
        }
    }

    /// <summary>ISender mínimo — ver a justificativa em CnjCaptureJobTests.DirectSender.</summary>
    private sealed class SenderDireto(CapturePublicationHandler handler, CreateAlertFromPublicationHandler alertHandler) : ISender
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            request switch
            {
                CapturePublicationCommand command => (TResponse)(object)await handler.Handle(command, cancellationToken),
                CreateAlertFromPublicationCommand command => (TResponse)(object)await alertHandler.Handle(command, cancellationToken),
                _ => throw new NotSupportedException($"SenderDireto não trata {request.GetType().Name}."),
            };

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    /// <summary>IAlertPublisher que só registra — o alerta real (Platform) tem testes próprios.</summary>
    internal sealed class AlertPublisherGravador : IAlertPublisher
    {
        private readonly List<AlertRequest> _criados = [];

        internal IReadOnlyList<AlertRequest> Criados => _criados;

        internal bool Falhar { get; set; }

        public Task<bool> PublishAsync(AlertRequest request, CancellationToken cancellationToken = default)
        {
            if (Falhar)
            {
                throw new InvalidOperationException("falha simulada do alerta");
            }

            _criados.Add(request);

            return Task.FromResult(true);
        }
    }

    private sealed class RelogioFixo(DateOnly hoje) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; } = new(hoje.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

        public DateOnly Today { get; } = hoje;
    }
}
