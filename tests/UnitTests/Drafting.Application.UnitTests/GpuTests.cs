using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class GpuStateServiceTests
{
    private static GpuStateService State(int falhas = 3, int pausaMin = 5) =>
        new(Options.Create(new VastAiOptions { CircuitBreakerFalhas = falhas, CircuitBreakerPausaMinutos = pausaMin }));

    [Fact]
    public void FalhasSeguidasAbremOCircuito_ESucessoOFecha()
    {
        var state = State();
        var agora = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

        state.RegistrarFalha("a", agora);
        state.RegistrarFalha("b", agora);
        state.CircuitoAberto(agora).Should().BeFalse();
        state.RegistrarFalha("c", agora);
        state.CircuitoAberto(agora).Should().BeTrue();

        state.RegistrarSucesso();
        state.CircuitoAberto(agora).Should().BeFalse();
    }

    [Fact]
    public void ApoisAPausa_DeixaPassarUmaTentativa_ESeFalharReabre()
    {
        var state = State(falhas: 2, pausaMin: 5);
        var t0 = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        state.RegistrarFalha("a", t0);
        state.RegistrarFalha("b", t0);
        state.CircuitoAberto(t0.AddMinutes(4)).Should().BeTrue();

        var depois = t0.AddMinutes(5);
        state.CircuitoAberto(depois).Should().BeFalse(); // meio-aberto: uma tentativa
        state.RegistrarFalha("c", depois);

        state.CircuitoAberto(depois).Should().BeTrue();
    }

    [Fact]
    public void RegistraOUltimoInicio()
    {
        var state = State();
        var agora = DateTimeOffset.UtcNow;

        state.RegistrarInicio(agora);

        state.UltimoInicio.Should().Be(agora);
        state.Snapshot(agora).Estado.Should().Be("Sob demanda");
    }
}

public class GpuOrchestratorTests
{
    private sealed class FakeVast : IVastAiService
    {
        public Queue<VastInstanceInfo> Estados { get; } = new();

        public VastInstanceInfo Atual { get; set; } = Info("exited");

        public bool ObterFalha { get; set; }

        public bool IniciarFalha { get; set; }

        public int Iniciar { get; private set; }

        public int Parar { get; private set; }

        public Task<Result<VastInstanceInfo>> ObterInstanciaAsync(CancellationToken cancellationToken = default)
        {
            if (ObterFalha)
            {
                return Task.FromResult(Result.Failure<VastInstanceInfo>(ErrorFactory.From(ErrorCode.DRAFTING_GPU_UNAVAILABLE)));
            }

            if (Estados.Count > 0)
            {
                Atual = Estados.Dequeue();
            }

            return Task.FromResult(Result.Success(Atual));
        }

        public Task<Result> IniciarAsync(string motivo, CancellationToken cancellationToken = default)
        {
            if (Atual.IsRunning)
            {
                return Task.FromResult(Result.Success());
            }

            Iniciar++;

            return Task.FromResult(IniciarFalha
                ? Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_GPU_UNAVAILABLE))
                : Result.Success());
        }

        public Task<Result> PararAsync(string motivo, CancellationToken cancellationToken = default)
        {
            Parar++;
            Atual = Info("exited");

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakeHealth : ILlmHealth
    {
        public Queue<bool> Saudes { get; } = new();

        public bool SaudePadrao { get; set; } = true;

        public IReadOnlyList<string> Modelos { get; set; } = ["qwen3:8b", "bge-m3:latest"];

        public int Chamadas { get; private set; }

        public Task<bool> VerificarSaudeAsync(CancellationToken cancellationToken = default)
        {
            Chamadas++;

            return Task.FromResult(Saudes.Count > 0 ? Saudes.Dequeue() : SaudePadrao);
        }

        public Task<IReadOnlyList<string>> ListarModelosAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Modelos);
    }

    private sealed class FakeEndpoint : IOllamaEndpoint
    {
        public Uri BaseAddress { get; private set; } = new("https://ia.exemplo.com.br/");

        public bool Rejeitar { get; set; }

        public void Set(Uri baseAddress)
        {
            if (Rejeitar)
            {
                throw new InvalidOperationException("http:// para host público");
            }

            BaseAddress = baseAddress;
        }
    }

    private sealed class Harness
    {
        public FakeVast Vast { get; } = new();

        public FakeHealth Health { get; } = new();

        public FakeEndpoint Endpoint { get; } = new();

        public FakeClock Clock { get; } = new();

        public FakeQueue Queue { get; }

        public FakeNotificacao Notificacao { get; } = new();

        public GpuStateService State { get; }

        public GpuOrchestrator Orchestrator { get; }

        public Harness(VastAiOptions? options = null)
        {
            Queue = new FakeQueue(Clock);
            var o = options ?? new VastAiOptions
            {
                PollingIntervalSeconds = 0, StartTimeoutMinutes = 10, IdleTimeoutMinutes = 10, CircuitBreakerFalhas = 3,
            };
            State = new GpuStateService(Options.Create(o));
            Orchestrator = new GpuOrchestrator(
                Vast, State, Queue, Health, Endpoint, Notificacao, Options.Create(o),
                Options.Create(new OllamaModelsOptions { Required = ["qwen3:8b", "bge-m3"] }),
                Clock, NullLogger<GpuOrchestrator>.Instance);
        }
    }

    private static VastInstanceInfo Info(string status, string? ip = null, int? port = null) =>
        new("123", status, null, ip, port is null ? new Dictionary<string, int>() : new Dictionary<string, int> { ["11434/tcp"] = port.Value });

    [Fact]
    public async Task GpuParada_IniciaEEsperaOllamaResponder_ChamandoOCallbackAntesDeEsperar()
    {
        var h = new Harness();
        h.Vast.Estados.Enqueue(Info("exited"));   // consulta inicial
        h.Vast.Estados.Enqueue(Info("loading"));  // ainda subindo
        h.Vast.Estados.Enqueue(Info("running"));  // ativa
        h.Health.Saudes.Enqueue(false);           // Ollama ainda não responde
        var callback = 0;

        var result = await h.Orchestrator.GarantirProntaAsync("1 item", _ => { callback++; return Task.CompletedTask; });

        result.IsSuccess.Should().BeTrue();
        h.Vast.Iniciar.Should().Be(1);
        callback.Should().Be(1);
        h.Health.Chamadas.Should().BeGreaterThanOrEqualTo(2);
        h.State.UltimoInicio.Should().NotBeNull();
    }

    [Fact]
    public async Task GpuJaAtiva_NaoIniciaNemChamaOCallback_SoConfereOOllama()
    {
        var h = new Harness();
        h.Vast.Atual = Info("running");
        var callback = 0;

        var result = await h.Orchestrator.GarantirProntaAsync("x", _ => { callback++; return Task.CompletedTask; });

        result.IsSuccess.Should().BeTrue();
        h.Vast.Iniciar.Should().Be(0);
        callback.Should().Be(0);
        h.Health.Chamadas.Should().Be(1);
    }

    [Fact]
    public async Task InstanciaQueNaoFicaAtivaNoPrazo_FalhaERegistraNoCircuito()
    {
        var h = new Harness(new VastAiOptions { PollingIntervalSeconds = 0, StartTimeoutMinutes = 0, CircuitBreakerFalhas = 1 });
        h.Vast.Atual = Info("loading");

        var result = await h.Orchestrator.GarantirProntaAsync("x", null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_GPU_UNAVAILABLE));
        h.State.CircuitoAberto(h.Clock.UtcNow).Should().BeTrue();
        h.Notificacao.Eventos.Should().Contain(e => e.StartsWith("operacao:"));
    }

    [Fact]
    public async Task OllamaQueNuncaResponde_FalhaDepoisDoPrazo()
    {
        var h = new Harness(new VastAiOptions { PollingIntervalSeconds = 0, StartTimeoutMinutes = 0 });
        h.Vast.Atual = Info("running");
        h.Health.SaudePadrao = false;

        var result = await h.Orchestrator.GarantirProntaAsync("x", null);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ModeloAusenteNoOllama_FalhaNaHoraComMensagemClara_SemEsperarOPrazo()
    {
        var h = new Harness();
        h.Vast.Atual = Info("running");
        h.Health.Modelos = ["qwen3:8b"]; // falta o bge-m3

        var result = await h.Orchestrator.GarantirProntaAsync("x", null);

        result.IsFailure.Should().BeTrue();
        h.Notificacao.Eventos.Should().Contain(e => e.Contains("bge-m3"));
        h.Health.Chamadas.Should().Be(1);
    }

    [Fact]
    public async Task FalhaAoIniciar_EhFalhaDaInfra()
    {
        var h = new Harness();
        h.Vast.IniciarFalha = true;

        var result = await h.Orchestrator.GarantirProntaAsync("x", null);

        result.IsFailure.Should().BeTrue();
        h.State.UltimoInicio.Should().BeNull();
    }

    [Fact]
    public async Task CircuitoAberto_NemTentaFalarComAVast()
    {
        var h = new Harness(new VastAiOptions { PollingIntervalSeconds = 0, CircuitBreakerFalhas = 1, CircuitBreakerPausaMinutos = 5 });
        h.State.RegistrarFalha("antes", h.Clock.UtcNow);
        h.Vast.ObterFalha = true;

        var result = await h.Orchestrator.GarantirProntaAsync("x", null);

        result.IsFailure.Should().BeTrue();
        h.Vast.Iniciar.Should().Be(0);
    }

    [Fact]
    public async Task ModoSemVast_SoEsperaOOllama_ENuncaDesliga()
    {
        var h = new Harness(new VastAiOptions { Enabled = false, PollingIntervalSeconds = 0 });
        h.Vast.ObterFalha = true; // se chamasse a Vast, falharia

        var ligar = await h.Orchestrator.GarantirProntaAsync("x", null);
        var parou = await h.Orchestrator.PararSeOciosaAsync();

        ligar.IsSuccess.Should().BeTrue();
        parou.Should().BeFalse();
        h.Vast.Parar.Should().Be(0);
    }

    [Fact]
    public async Task EndpointDescobertoPelaVast_AtualizaOEnderecoDoOllama_QuandoPermitido()
    {
        var h = new Harness(new VastAiOptions { PollingIntervalSeconds = 0, ResolverEndpointPelaVast = true, PortaOllama = 11434 });
        h.Vast.Atual = Info("running", "203.0.113.9", 41234);

        var result = await h.Orchestrator.GarantirProntaAsync("x", null);

        result.IsSuccess.Should().BeTrue();
        h.Endpoint.BaseAddress.Should().Be(new Uri("http://203.0.113.9:41234/"));
    }

    [Fact]
    public async Task EndpointDescobertoRecusadoPeloGuardDeTransporte_FalhaEAvisaAOperacao()
    {
        var h = new Harness(new VastAiOptions { PollingIntervalSeconds = 0, ResolverEndpointPelaVast = true });
        h.Vast.Atual = Info("running", "203.0.113.9", 41234);
        h.Endpoint.Rejeitar = true;

        var result = await h.Orchestrator.GarantirProntaAsync("x", null);

        result.IsFailure.Should().BeTrue();
        h.Notificacao.Eventos.Should().Contain(e => e.Contains("http://"));
    }

    [Fact]
    public async Task PortaDoOllamaNaoMapeada_Falha()
    {
        var h = new Harness(new VastAiOptions { PollingIntervalSeconds = 0, ResolverEndpointPelaVast = true });
        h.Vast.Atual = Info("running", "203.0.113.9");

        var result = await h.Orchestrator.GarantirProntaAsync("x", null);

        result.IsFailure.Should().BeTrue();
    }

    // ------------------------------------------------------------ parar por ociosidade

    [Fact]
    public async Task ComTrabalhoNaFila_NaoPara()
    {
        var h = new Harness();
        h.Vast.Atual = Info("running");
        h.Queue.Enfileirar(Ids.Tenant, Domain.Fila.QueueItemKind.Geracao, Guid.NewGuid());
        h.Clock.Advance(TimeSpan.FromHours(2));

        (await h.Orchestrator.PararSeOciosaAsync()).Should().BeFalse();
        h.Vast.Parar.Should().Be(0);
    }

    [Fact]
    public async Task DentroDaCarencia_NaoPara_DepoisDelaPara()
    {
        var h = new Harness();
        h.Vast.Atual = Info("running");
        h.Queue.Enfileirar(Ids.Tenant, Domain.Fila.QueueItemKind.Geracao, Guid.NewGuid());
        await h.Queue.ReservarAsync("w", 1, TimeSpan.FromMinutes(15));
        await h.Queue.FinalizarAsync(h.Queue.Items.Single().Id, Domain.Fila.QueueItemStatus.Concluido, null);

        h.Clock.Advance(TimeSpan.FromMinutes(9));
        (await h.Orchestrator.PararSeOciosaAsync()).Should().BeFalse("ainda dentro dos 10 min de carência");

        h.Clock.Advance(TimeSpan.FromMinutes(2));
        (await h.Orchestrator.PararSeOciosaAsync()).Should().BeTrue();
        h.Vast.Parar.Should().Be(1);
    }

    [Fact]
    public async Task InstanciaJaParada_NaoChamaPararDeNovo()
    {
        var h = new Harness();
        h.Vast.Atual = Info("exited");
        h.Clock.Advance(TimeSpan.FromHours(1));

        (await h.Orchestrator.PararSeOciosaAsync()).Should().BeFalse();
        h.Vast.Parar.Should().Be(0);
    }

    [Fact]
    public async Task FilaVaziaECarenciaVencida_Para_SemNuncaDestruir()
    {
        var h = new Harness();
        h.Vast.Atual = Info("running");

        (await h.Orchestrator.PararSeOciosaAsync()).Should().BeTrue();
        // A interface só tem Parar: não existe operação de destruir para chamar.
        typeof(IVastAiService).GetMethods().Select(m => m.Name).Should().NotContain(n => n.Contains("Destr"));
    }

    [Theory]
    [InlineData("qwen3:8b", "qwen3:8b", true)]
    [InlineData("bge-m3:latest", "bge-m3", true)]
    [InlineData("QWEN3:8B", "qwen3:8b", true)]
    [InlineData("qwen3:14b", "qwen3:8b", false)]
    [InlineData("bge-m3:latest", "bge-m3:fp16", false)]
    public void ModelMatches_ConsideraOSufixoLatestSoQuandoOModeloPedidoNaoTemTag(string instalado, string pedido, bool esperado)
    {
        GpuOrchestrator.ModelMatches(instalado, pedido).Should().Be(esperado);
    }
}

public class EmbeddingCacheTests
{
    private sealed class CountingEmbeddings : IEmbeddingService
    {
        public List<string> Pedidos { get; } = [];

        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
        {
            Pedidos.AddRange(inputs);

            return Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(t => new[] { (float)t.Length }).ToList());
        }
    }

    [Fact]
    public async Task OMesmoConteudoNaoPagaGpuDuasVezes_ESoOQueFaltaVaiAoOllama()
    {
        var inner = new CountingEmbeddings();
        var cached = new CachedEmbeddingService(inner, new EmbeddingCache());

        await cached.EmbedAsync(["Texto A", "Texto B"]);
        var segunda = await cached.EmbedAsync(["texto   a", "Texto C"]); // "texto   a" = mesmo conteúdo normalizado

        inner.Pedidos.Should().Equal("Texto A", "Texto B", "Texto C");
        segunda.Should().HaveCount(2);
        segunda[0].Should().Equal(7f);
    }

    [Fact]
    public async Task OrdemDaRespostaSegueAOrdemDoPedido_MesmoMisturandoCacheEFalta()
    {
        var inner = new CountingEmbeddings();
        var cached = new CachedEmbeddingService(inner, new EmbeddingCache());
        await cached.EmbedAsync(["bb"]);

        var result = await cached.EmbedAsync(["a", "bb", "cccc"]);

        result.Select(v => v[0]).Should().Equal(1f, 2f, 4f);
    }

    [Fact]
    public void CacheDescartaOMaisAntigoAoEncher()
    {
        var cache = new EmbeddingCache(capacidade: 2);

        cache.Set("a", [1f]);
        cache.Set("b", [2f]);
        cache.Set("c", [3f]);

        cache.Count.Should().Be(2);
        cache.TryGet("a", out _).Should().BeFalse();
        cache.TryGet("c", out _).Should().BeTrue();
    }
}
