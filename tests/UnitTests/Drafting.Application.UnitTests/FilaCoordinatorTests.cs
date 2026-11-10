using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Fila;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class FilaCoordinatorTests
{
    private sealed class FakeOrchestrator : IGpuOrchestrator
    {
        public int Garantir { get; private set; }

        public int Parar { get; private set; }

        public bool Falhar { get; set; }

        public bool InvocarCallbackAoIniciar { get; set; } = true;

        public Task<Result> GarantirProntaAsync(string motivo, Func<CancellationToken, Task>? aoIniciar, CancellationToken cancellationToken = default)
        {
            Garantir++;
            if (Falhar)
            {
                return Task.FromResult(Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_GPU_UNAVAILABLE)));
            }

            return InvocarCallbackAoIniciar && aoIniciar is not null
                ? RunAsync(aoIniciar, cancellationToken)
                : Task.FromResult(Result.Success());
        }

        public Task<bool> PararSeOciosaAsync(CancellationToken cancellationToken = default)
        {
            Parar++;

            return Task.FromResult(false);
        }

        private static async Task<Result> RunAsync(Func<CancellationToken, Task> aoIniciar, CancellationToken ct)
        {
            await aoIniciar(ct);

            return Result.Success();
        }
    }

    private sealed class RecordingHandler(QueueItemKind tipo, List<string> log) : IQueueWorkHandler
    {
        public QueueItemKind Tipo => tipo;

        public Func<Guid, WorkOutcome> Outcome { get; set; } = _ => new WorkOutcome(WorkOutcomeKind.Concluido);

        public Exception? Throw { get; set; }

        public Task AoReservarAsync(Guid referenciaId, CancellationToken cancellationToken = default)
        {
            log.Add($"{tipo}:reservar");

            return Task.CompletedTask;
        }

        public Task AoPrepararGpuAsync(Guid referenciaId, CancellationToken cancellationToken = default)
        {
            log.Add($"{tipo}:preparar");

            return Task.CompletedTask;
        }

        public Task AoFaltarInfraAsync(Guid referenciaId, string motivo, CancellationToken cancellationToken = default)
        {
            log.Add($"{tipo}:faltou-infra");

            return Task.CompletedTask;
        }

        public Task<WorkOutcome> ProcessarAsync(Guid referenciaId, int tentativa, CancellationToken cancellationToken = default)
        {
            log.Add($"{tipo}:processar:{tentativa}");
            if (Throw is not null)
            {
                throw Throw;
            }

            return Task.FromResult(Outcome(referenciaId));
        }

        public Task AoExpirarAsync(Guid referenciaId, CancellationToken cancellationToken = default)
        {
            log.Add($"{tipo}:expirar");

            return Task.CompletedTask;
        }

        public Task AoFalharDefinitivoAsync(Guid referenciaId, string erro, CancellationToken cancellationToken = default)
        {
            log.Add($"{tipo}:falhar-definitivo");

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTenantContext : ITenantContext
    {
        public List<Guid> Tenants { get; } = [];

        public bool HasTenant => Tenants.Count > 0;

        public Guid TenantId => Tenants[^1];

        public void SetTenant(Guid tenantId)
        {
            lock (Tenants)
            {
                Tenants.Add(tenantId);
            }
        }
    }

    private sealed class Harness
    {
        public FakeClock Clock { get; } = new();

        public FakeQueue Queue { get; }

        public FakeOrchestrator Orchestrator { get; } = new();

        public List<string> Log { get; } = [];

        public RecordingHandler Geracao { get; }

        public RecordingHandler Indexacao { get; }

        public RecordingTenantContext Tenants { get; } = new();

        public FilaCoordinator Coordinator { get; }

        public Harness(FilaOptions? options = null)
        {
            Queue = new FakeQueue(Clock);
            Geracao = new RecordingHandler(QueueItemKind.Geracao, Log);
            Indexacao = new RecordingHandler(QueueItemKind.Indexacao, Log);

            var services = new ServiceCollection();
            services.AddSingleton<IGeracaoPecaFila>(Queue);
            services.AddSingleton<IGpuOrchestrator>(Orchestrator);
            services.AddSingleton<IQueueWorkHandler>(Geracao);
            services.AddSingleton<IQueueWorkHandler>(Indexacao);
            services.AddSingleton<ITenantContext>(Tenants);
            var provider = services.BuildServiceProvider();

            Coordinator = new FilaCoordinator(
                provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(options ?? new FilaOptions { LoteMax = 5, MaxTentativas = 3, BackoffBaseSeconds = 30, ExpiraAposMinutos = 60 }),
                Clock,
                NullLogger<FilaCoordinator>.Instance);
        }

        public Guid Enqueue(QueueItemKind tipo = QueueItemKind.Geracao, Guid? tenant = null)
        {
            var reference = Guid.NewGuid();
            Queue.Enfileirar(tenant ?? Ids.Tenant, tipo, reference);

            return reference;
        }
    }

    [Fact]
    public async Task SemTrabalho_NaoLigaAGpu_EDeixaAPoliticaDeOciosidadeDecidirSePara()
    {
        var h = new Harness();

        var worked = await h.Coordinator.RunCycleAsync(CancellationToken.None);

        worked.Should().BeFalse();
        h.Orchestrator.Garantir.Should().Be(0);
        h.Orchestrator.Parar.Should().Be(1);
    }

    [Fact]
    public async Task ComTrabalho_ReservaLigaAGpuEProcessaEFinaliza()
    {
        var h = new Harness();
        h.Enqueue();

        var worked = await h.Coordinator.RunCycleAsync(CancellationToken.None);

        worked.Should().BeTrue();
        h.Log.Should().Equal("Geracao:reservar", "Geracao:preparar", "Geracao:processar:1");
        h.Queue.Items.Single().Status.Should().Be(QueueItemStatus.Concluido);
        // Com trabalho, não pergunta se a GPU está ociosa neste ciclo.
        h.Orchestrator.Parar.Should().Be(0);
    }

    [Fact]
    public async Task UmLoteLigaAGpuUmaUnicaVez()
    {
        var h = new Harness();
        for (var i = 0; i < 4; i++)
        {
            h.Enqueue();
        }

        await h.Coordinator.RunCycleAsync(CancellationToken.None);

        h.Orchestrator.Garantir.Should().Be(1);
        h.Log.Count(l => l == "Geracao:processar:1").Should().Be(4);
        h.Queue.Items.Should().OnlyContain(i => i.Status == QueueItemStatus.Concluido);
    }

    [Fact]
    public async Task GpuJaPronta_NaoMostraPreparandoIa()
    {
        var h = new Harness();
        h.Orchestrator.InvocarCallbackAoIniciar = false; // a instância já estava ativa: o callback não é chamado
        h.Enqueue();

        await h.Coordinator.RunCycleAsync(CancellationToken.None);

        h.Log.Should().NotContain("Geracao:preparar");
    }

    [Fact]
    public async Task ProcessaDoisTiposDeTrabalhoNaMesmaSubidaDaGpu()
    {
        var h = new Harness();
        h.Enqueue(QueueItemKind.Geracao);
        h.Enqueue(QueueItemKind.Indexacao);

        await h.Coordinator.RunCycleAsync(CancellationToken.None);

        h.Orchestrator.Garantir.Should().Be(1);
        h.Log.Should().Contain("Geracao:processar:1").And.Contain("Indexacao:processar:1");
    }

    [Fact]
    public async Task GpuIndisponivel_DevolveOLoteSemGastarTentativaENaoProcessa()
    {
        var h = new Harness();
        h.Orchestrator.Falhar = true;
        h.Enqueue();

        var worked = await h.Coordinator.RunCycleAsync(CancellationToken.None);

        worked.Should().BeTrue();
        h.Log.Should().Contain("Geracao:faltou-infra").And.NotContain("Geracao:processar:1");
        var item = h.Queue.Items.Single();
        item.Status.Should().Be(QueueItemStatus.Pendente);
        item.Tentativas.Should().Be(0);
        item.DisponivelEm.Should().BeAfter(h.Clock.UtcNow);
        // E não insiste no mesmo ciclo: uma única tentativa de ligar.
        h.Orchestrator.Garantir.Should().Be(1);
    }

    [Fact]
    public async Task FalhaTransitoria_ReagendaComBackoffExponencialConsumindoTentativa()
    {
        var h = new Harness();
        h.Geracao.Outcome = _ => new WorkOutcome(WorkOutcomeKind.Reagendar, "timeout");
        h.Enqueue();

        await h.Coordinator.RunCycleAsync(CancellationToken.None);

        var item = h.Queue.Items.Single();
        item.Status.Should().Be(QueueItemStatus.Pendente);
        item.Tentativas.Should().Be(1);
        item.DisponivelEm.Should().Be(h.Clock.UtcNow + TimeSpan.FromSeconds(30));
        item.UltimoErro.Should().Be("timeout");
        // Em backoff: não é elegível agora, então o mesmo ciclo não a reprocessa.
        h.Log.Count(l => l.StartsWith("Geracao:processar")).Should().Be(1);

        h.Clock.Advance(TimeSpan.FromSeconds(31));
        await h.Coordinator.RunCycleAsync(CancellationToken.None);

        h.Queue.Items.Single().Tentativas.Should().Be(2);
        h.Queue.Items.Single().DisponivelEm.Should().Be(h.Clock.UtcNow + TimeSpan.FromSeconds(60));
    }

    [Theory]
    [InlineData(WorkOutcomeKind.Falhou, QueueItemStatus.Falhou)]
    [InlineData(WorkOutcomeKind.Cancelado, QueueItemStatus.Cancelado)]
    [InlineData(WorkOutcomeKind.Concluido, QueueItemStatus.Concluido)]
    public async Task ResultadosDefinitivos_EncerramOItem(WorkOutcomeKind resultado, QueueItemStatus esperado)
    {
        var h = new Harness();
        h.Geracao.Outcome = _ => new WorkOutcome(resultado, "motivo");
        h.Enqueue();

        await h.Coordinator.RunCycleAsync(CancellationToken.None);

        var item = h.Queue.Items.Single();
        item.Status.Should().Be(esperado);
        item.ConcluidoEm.Should().NotBeNull();
        h.Log.Count(l => l.StartsWith("Geracao:processar")).Should().Be(1);
    }

    [Fact]
    public async Task Interrompido_VoltaAFilaSemGastarTentativa()
    {
        var h = new Harness();
        h.Geracao.Outcome = _ => new WorkOutcome(WorkOutcomeKind.Interrompido, "desligando");
        h.Enqueue();
        using var cts = new CancellationTokenSource();

        // Reservar+processar uma vez e parar antes de reprocessar.
        h.Geracao.Outcome = _ =>
        {
            cts.Cancel();

            return new WorkOutcome(WorkOutcomeKind.Interrompido, "desligando");
        };
        await h.Coordinator.RunCycleAsync(cts.Token);

        var item = h.Queue.Items.Single();
        item.Status.Should().Be(QueueItemStatus.Pendente);
        item.Tentativas.Should().Be(0);
    }

    [Fact]
    public async Task ExcecaoInesperadaDoHandler_FalhaOItemEAvisaOHandler_SemDerrubarOCiclo()
    {
        var h = new Harness();
        h.Geracao.Throw = new InvalidOperationException("bug");
        h.Enqueue();
        var outro = h.Enqueue(QueueItemKind.Indexacao);

        var act = () => h.Coordinator.RunCycleAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        h.Log.Should().Contain("Geracao:falhar-definitivo");
        h.Queue.Items.First(i => i.Tipo == QueueItemKind.Geracao).Status.Should().Be(QueueItemStatus.Falhou);
        h.Queue.Items.First(i => i.ReferenciaId == outro).Status.Should().Be(QueueItemStatus.Concluido);
    }

    [Fact]
    public async Task ItemQueEsperouDemais_ExpiraSemUsarAGpu()
    {
        var h = new Harness();
        h.Enqueue();
        h.Clock.Advance(TimeSpan.FromMinutes(61));

        await h.Coordinator.RunCycleAsync(CancellationToken.None);

        h.Log.Should().Contain("Geracao:expirar");
        h.Log.Should().NotContain("Geracao:processar:1");
        h.Orchestrator.Garantir.Should().Be(0);
        h.Queue.Items.Single().Status.Should().Be(QueueItemStatus.Falhou);
    }

    [Fact]
    public async Task CadaItemRodaComOTenantDaSolicitacao()
    {
        var h = new Harness();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        h.Enqueue(tenant: tenantA);
        h.Enqueue(tenant: tenantB);

        await h.Coordinator.RunCycleAsync(CancellationToken.None);

        h.Tenants.Tenants.Should().Contain(tenantA).And.Contain(tenantB);
    }

    [Fact]
    public async Task ReservaVencida_OWorkerQueCaiuNaoPrendeOItem()
    {
        var h = new Harness();
        h.Enqueue();
        var primeiro = await h.Queue.ReservarAsync("worker-que-caiu", 5, TimeSpan.FromMinutes(15));
        primeiro.Should().HaveCount(1);
        (await h.Queue.ReservarAsync("outro", 5, TimeSpan.FromMinutes(15))).Should().BeEmpty(); // ainda reservado

        h.Clock.Advance(TimeSpan.FromMinutes(16));
        await h.Coordinator.RunCycleAsync(CancellationToken.None);

        h.Log.Should().Contain("Geracao:processar:2"); // 2ª reserva = 2ª tentativa
        h.Queue.Items.Single().Status.Should().Be(QueueItemStatus.Concluido);
    }

    [Fact]
    public async Task DoisWorkersSobreAMesmaFila_NaoProcessamOMesmoItemDuasVezes()
    {
        var h = new Harness();
        for (var i = 0; i < 6; i++)
        {
            h.Enqueue();
        }

        var primeiro = await h.Queue.ReservarAsync("a", 4, TimeSpan.FromMinutes(15));
        var segundo = await h.Queue.ReservarAsync("b", 4, TimeSpan.FromMinutes(15));

        primeiro.Should().HaveCount(4);
        segundo.Should().HaveCount(2);
        primeiro.Select(i => i.Id).Intersect(segundo.Select(i => i.Id)).Should().BeEmpty();
    }
}
