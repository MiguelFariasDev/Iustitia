using System.Net;
using System.Text;
using System.Text.Json;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Gpu;
using Advocacia.Modules.Drafting.Infrastructure.VastAi;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.UnitTests;

public class VastAiServiceTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri, string Body, string? Auth)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.Method, request.RequestUri!, body, request.Headers.Authorization?.ToString()));

            return respond(request, body);
        }
    }

    private sealed class FakeLogs : IGpuInstanceLogRepository
    {
        public List<GpuInstanceLog> Items { get; } = [];

        public void Add(GpuInstanceLog log) => Items.Add(log);

        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

        public DateOnly Today => new(2026, 11, 10);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string Instance(string status, string? ports = null) =>
        "{\"instances\":{\"id\":123,\"actual_status\":\"" + status + "\",\"intended_status\":\"" + status +
        "\",\"public_ipaddr\":\"203.0.113.9\",\"ports\":" + (ports ?? "{\"11434/tcp\":[{\"HostIp\":\"0.0.0.0\",\"HostPort\":\"41234\"}]}") + "}}";

    private sealed record Setup(VastAiService Service, StubHandler Handler, FakeLogs Logs, GpuStateService State);

    private static Setup Create(Func<HttpRequestMessage, string, HttpResponseMessage> respond, Action<VastAiOptions>? tweak = null)
    {
        var options = new VastAiOptions
        {
            ApiBaseUrl = "https://console.vast.ai/api/v0",
            ApiKey = "chave-secreta",
            InstanceId = "123",
            MaxRetries = 3,
            RetryDelayMilliseconds = 1,
            CircuitBreakerFalhas = 3,
        };
        tweak?.Invoke(options);

        var handler = new StubHandler(respond);
        var logs = new FakeLogs();
        var state = new GpuStateService(Options.Create(options));
        var service = new VastAiService(
            new HttpClient(handler), Options.Create(options), logs, state, new FixedClock(), NullLogger<VastAiService>.Instance);

        return new Setup(service, handler, logs, state);
    }

    [Fact]
    public async Task Obter_UsaAUrlEoBearerDocumentadosDaVast_ELeEstadoIpEPortas()
    {
        var s = Create((_, _) => Json(Instance("running")));

        var result = await s.Service.ObterInstanciaAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.IsRunning.Should().BeTrue();
        result.Value.PublicIp.Should().Be("203.0.113.9");
        result.Value.PublicPorts["11434/tcp"].Should().Be(41234);
        var call = s.Handler.Calls.Single();
        call.Method.Should().Be(HttpMethod.Get);
        call.Uri.ToString().Should().Be("https://console.vast.ai/api/v0/instances/123/?owner=me");
        call.Auth.Should().Be("Bearer chave-secreta");
    }

    [Theory]
    [InlineData("exited", false, true)]
    [InlineData("running", true, false)]
    [InlineData("loading", false, false)]
    public async Task Obter_ClassificaOEstado(string status, bool rodando, bool parada)
    {
        var s = Create((_, _) => Json(Instance(status)));

        var info = (await s.Service.ObterInstanciaAsync()).Value;

        info.IsRunning.Should().Be(rodando);
        info.IsStopped.Should().Be(parada);
    }

    [Fact]
    public async Task Obter_InstanciaInexistente_EhIndisponivel()
    {
        var s = Create((_, _) => Json("{\"instances\":null}"));

        var result = await s.Service.ObterInstanciaAsync();

        result.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_GPU_UNAVAILABLE));
    }

    [Fact]
    public async Task Iniciar_InstanciaParada_FazPutComStateRunning_ERegistraOLog()
    {
        var s = Create((req, _) => req.Method == HttpMethod.Put
            ? Json("{\"success\":true}")
            : Json(Instance("exited")));

        var result = await s.Service.IniciarAsync("2 itens na fila");

        result.IsSuccess.Should().BeTrue();
        var put = s.Handler.Calls.Single(c => c.Method == HttpMethod.Put);
        put.Uri.ToString().Should().Be("https://console.vast.ai/api/v0/instances/123/");
        using var body = JsonDocument.Parse(put.Body);
        body.RootElement.GetProperty("state").GetString().Should().Be("running");
        var log = s.Logs.Items.Single();
        log.Acao.Should().Be("Iniciar");
        log.StatusAnterior.Should().Be("exited");
        log.StatusNovo.Should().Be("running");
        log.Motivo.Should().Be("2 itens na fila");
        log.Sucesso.Should().BeTrue();
        log.VastInstanceId.Should().Be("123");
    }

    [Fact]
    public async Task Iniciar_InstanciaJaAtiva_NaoChamaAApi()
    {
        var s = Create((_, _) => Json(Instance("running")));

        var result = await s.Service.IniciarAsync("x");

        result.IsSuccess.Should().BeTrue();
        s.Handler.Calls.Should().OnlyContain(c => c.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task Parar_InstanciaAtiva_FazPutComStateStopped_NuncaDelete()
    {
        var s = Create((req, _) => req.Method == HttpMethod.Put ? Json("{\"success\":true}") : Json(Instance("running")));

        var result = await s.Service.PararAsync("ociosa");

        result.IsSuccess.Should().BeTrue();
        using var body = JsonDocument.Parse(s.Handler.Calls.Single(c => c.Method == HttpMethod.Put).Body);
        body.RootElement.GetProperty("state").GetString().Should().Be("stopped");
        s.Handler.Calls.Should().NotContain(c => c.Method == HttpMethod.Delete);
        s.Logs.Items.Single().Acao.Should().Be("Parar");
    }

    [Fact]
    public async Task Parar_InstanciaJaParada_NaoChamaAApi()
    {
        var s = Create((_, _) => Json(Instance("exited")));

        (await s.Service.PararAsync("x")).IsSuccess.Should().BeTrue();

        s.Handler.Calls.Should().OnlyContain(c => c.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task VastRecusaAOperacao_VirgaFalhaERegistraOMotivoNoLog()
    {
        var s = Create((req, _) => req.Method == HttpMethod.Put
            ? Json("{\"success\":false,\"msg\":\"resources unavailable\"}")
            : Json(Instance("exited")));

        var result = await s.Service.IniciarAsync("x");

        result.IsFailure.Should().BeTrue();
        var log = s.Logs.Items.Single();
        log.Sucesso.Should().BeFalse();
        log.Erro.Should().Be("resources unavailable");
    }

    [Fact]
    public async Task Http401_NaoEhRepetido()
    {
        var s = Create((_, _) => Json("{}", HttpStatusCode.Unauthorized));

        var result = await s.Service.ObterInstanciaAsync();

        result.IsFailure.Should().BeTrue();
        s.Handler.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Http500_ERepetidoAteOLimite()
    {
        var s = Create((_, _) => Json("{}", HttpStatusCode.InternalServerError));

        var result = await s.Service.ObterInstanciaAsync();

        result.IsFailure.Should().BeTrue();
        s.Handler.Calls.Should().HaveCount(3);
    }

    [Fact]
    public async Task FalhaTransitoriaSeguidaDeSucesso_Recupera()
    {
        var chamadas = 0;
        var s = Create((_, _) => ++chamadas == 1 ? Json("{}", HttpStatusCode.ServiceUnavailable) : Json(Instance("running")));

        var result = await s.Service.ObterInstanciaAsync();

        result.IsSuccess.Should().BeTrue();
        chamadas.Should().Be(2);
    }

    [Fact]
    public async Task Http429_TambemERepetido()
    {
        var chamadas = 0;
        var s = Create((_, _) => ++chamadas < 3 ? Json("{}", (HttpStatusCode)429) : Json(Instance("running")));

        (await s.Service.ObterInstanciaAsync()).IsSuccess.Should().BeTrue();
        chamadas.Should().Be(3);
    }

    [Fact]
    public async Task FalhaDeRede_ERepetida_EDepoisVirgaIndisponivel()
    {
        var s = Create((_, _) => throw new HttpRequestException("connection refused"));

        var result = await s.Service.ObterInstanciaAsync();

        result.Error.Code.Should().Be(nameof(ErrorCode.DRAFTING_GPU_UNAVAILABLE));
        s.Handler.Calls.Should().HaveCount(3);
    }

    [Fact]
    public async Task FalhasSeguidasAbremOCircuito()
    {
        var s = Create((_, _) => Json("{}", HttpStatusCode.InternalServerError), o => { o.MaxRetries = 1; o.CircuitBreakerFalhas = 2; });

        await s.Service.ObterInstanciaAsync();
        s.State.CircuitoAberto(DateTimeOffset.UtcNow).Should().BeFalse();
        await s.Service.ObterInstanciaAsync();

        s.State.CircuitoAberto(new FixedClock().UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task SemChaveOuSemInstancia_FalhaSemChamarARede()
    {
        var semChave = Create((_, _) => Json(Instance("running")), o => o.ApiKey = string.Empty);
        var semInstancia = Create((_, _) => Json(Instance("running")), o => o.InstanceId = " ");

        (await semChave.Service.ObterInstanciaAsync()).IsFailure.Should().BeTrue();
        (await semInstancia.Service.ObterInstanciaAsync()).IsFailure.Should().BeTrue();
        semChave.Handler.Calls.Should().BeEmpty();
        semInstancia.Handler.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ChaveDaVast_SoTrafegaPorTls()
    {
        var s = Create((_, _) => Json(Instance("running")), o => o.ApiBaseUrl = "http://console.vast.ai/api/v0");

        (await s.Service.ObterInstanciaAsync()).IsFailure.Should().BeTrue();
        s.Handler.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ACancelamentoDoChamador_PropagaSemVirarFalhaDaVast()
    {
        var s = Create((_, _) => throw new TaskCanceledException());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => s.Service.ObterInstanciaAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Parse_IgnoraPortasMalformadas_ESeguemAsBoas()
    {
        using var doc = JsonDocument.Parse(
            "{\"instances\":{\"id\":1,\"actual_status\":\"running\",\"ports\":{\"22/tcp\":[],\"1/tcp\":\"x\",\"11434/tcp\":[{\"HostPort\":\"5555\"}]}}}");

        var info = VastAiService.Parse(doc.RootElement)!;

        info.PublicPorts.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, int>("11434/tcp", 5555));
    }
}
