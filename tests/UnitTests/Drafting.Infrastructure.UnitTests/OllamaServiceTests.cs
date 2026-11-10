using System.Net;
using System.Text;
using System.Text.Json;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Infrastructure.Ollama;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.UnitTests;

public class OllamaServiceTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request, body));

            return await respond(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Ndjson(params string[] lines) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Join('\n', lines) + "\n", Encoding.UTF8, "application/x-ndjson"),
        };

    private static string Line(string content, bool done = false, int? prompt = null, int? output = null) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["model"] = "qwen3:8b",
            ["message"] = new { role = "assistant", content },
            ["done"] = done,
            ["prompt_eval_count"] = prompt,
            ["eval_count"] = output,
        });

    private static OllamaService Service(StubHandler handler, Action<OllamaOptions>? tweak = null)
    {
        var options = new OllamaOptions { BaseUrl = "http://localhost:11434", TimeoutSegundos = 30 };
        tweak?.Invoke(options);

        var endpoint = new OllamaEndpoint(Options.Create(options));

        return new OllamaService(
            new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan },
            endpoint,
            Options.Create(options),
            NullLogger<OllamaService>.Instance);
    }

    private static LlmChatRequest Chat() =>
        new([new LlmMessage("system", "regras"), new LlmMessage("user", "caso")]);

    private static async Task<List<LlmChunk>> Collect(IAsyncEnumerable<LlmChunk> stream, CancellationToken ct = default)
    {
        var list = new List<LlmChunk>();
        await foreach (var chunk in stream.WithCancellation(ct))
        {
            list.Add(chunk);
        }

        return list;
    }

    [Fact]
    public async Task ParseiaNdjsonEmFragmentosEUmChunkFinalComContagemDeTokens()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Ndjson(
            Line("Olá"), Line(" mundo"), Line(string.Empty, done: true, prompt: 12, output: 34))));

        var chunks = await Collect(Service(handler).ChatStreamAsync(Chat()));

        chunks.Select(c => c.Content).Should().Equal("Olá", " mundo", string.Empty);
        chunks.Take(2).Should().OnlyContain(c => !c.Done);
        var final = chunks[^1];
        final.Done.Should().BeTrue();
        final.PromptTokens.Should().Be(12);
        final.OutputTokens.Should().Be(34);
    }

    [Fact]
    public async Task EnviaModeloMensagensStreamThinkFalseEJanelaDeContexto()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Ndjson(Line(string.Empty, done: true))));

        await Collect(Service(handler).ChatStreamAsync(Chat()));

        var (request, body) = handler.Calls.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.AbsolutePath.Should().Be("/api/chat");
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        root.GetProperty("model").GetString().Should().Be("qwen3:8b");
        root.GetProperty("stream").GetBoolean().Should().BeTrue();
        root.GetProperty("think").GetBoolean().Should().BeFalse();
        root.GetProperty("keep_alive").GetString().Should().Be("10m");
        root.GetProperty("options").GetProperty("num_ctx").GetInt32().Should().Be(16384);
        root.GetProperty("options").GetProperty("temperature").GetDouble().Should().Be(0.2);
        root.GetProperty("options").GetProperty("top_p").GetDouble().Should().Be(0.9);
        root.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("role").GetString())
            .Should().Equal("system", "user");
    }

    [Fact]
    public async Task EnviaABearerQuandoHaChaveDeProxy_ENaoEnviaQuandoNao()
    {
        var comChave = new StubHandler((_, _) => Task.FromResult(Ndjson(Line(string.Empty, done: true))));
        var semChave = new StubHandler((_, _) => Task.FromResult(Ndjson(Line(string.Empty, done: true))));

        await Collect(Service(comChave, o => o.ApiKey = "segredo").ChatStreamAsync(Chat()));
        await Collect(Service(semChave).ChatStreamAsync(Chat()));

        comChave.Calls[0].Request.Headers.Authorization!.ToString().Should().Be("Bearer segredo");
        semChave.Calls[0].Request.Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task RemoveOBlocoThinkMesmoComATagPartidaEntreFragmentos()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Ndjson(
            Line("<thi"), Line("nk>raciocínio secreto</th"), Line("ink>\n\nDOS FATOS"), Line(" ok"),
            Line(string.Empty, done: true))));

        var chunks = await Collect(Service(handler).ChatStreamAsync(Chat()));

        string.Concat(chunks.Select(c => c.Content)).Should().Be("DOS FATOS ok");
    }

    [Fact]
    public async Task RespostaHttpDeErro_VirgaLlmServiceExceptionSemVazarOCorpo()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("model 'qwen3:8b' not found: DADO-DO-PROMPT"),
        }));

        var act = () => Collect(Service(handler).ChatStreamAsync(Chat()));

        var erro = (await act.Should().ThrowAsync<LlmServiceException>()).Which;
        erro.Message.Should().Contain("500").And.NotContain("DADO-DO-PROMPT");
    }

    [Fact]
    public async Task FalhaDeRede_VirgaLlmServiceException()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("connection refused"));

        var act = () => Collect(Service(handler).ChatStreamAsync(Chat()));

        await act.Should().ThrowAsync<LlmServiceException>().WithMessage("*conectar ao Ollama*");
    }

    [Fact]
    public async Task EstourarOPrazo_VirgaTimeoutException_NaoCancelamento()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);

            return Ndjson();
        });

        var act = () => Collect(Service(handler, o => o.TimeoutSegundos = 1).ChatStreamAsync(Chat()));

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task CancelamentoDoChamador_PropagaOperationCanceled_NaoTimeout()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);

            return Ndjson();
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var act = () => Collect(Service(handler).ChatStreamAsync(Chat(), cts.Token));

        var erro = await act.Should().ThrowAsync<OperationCanceledException>();
        erro.Which.Should().NotBeOfType<TimeoutException>();
    }

    [Fact]
    public async Task CancelamentoNoMeioDoStream_InterrompeALeitura()
    {
        using var cts = new CancellationTokenSource();
        var handler = new StubHandler((_, _) => Task.FromResult(Ndjson(Line("a"), Line("b"), Line("c"), Line(string.Empty, done: true))));
        var recebidos = new List<string>();

        var act = async () =>
        {
            await foreach (var chunk in Service(handler).ChatStreamAsync(Chat(), cts.Token))
            {
                recebidos.Add(chunk.Content);
                cts.Cancel();
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        recebidos.Should().HaveCountLessThan(4);
    }

    [Fact]
    public async Task LinhaDeErroNoMeioDoStream_VirgaLlmServiceException()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Ndjson(Line("a"), "{\"error\":\"out of memory\"}")));

        var act = () => Collect(Service(handler).ChatStreamAsync(Chat()));

        await act.Should().ThrowAsync<LlmServiceException>().WithMessage("*out of memory*");
    }

    [Fact]
    public async Task StreamQueFechaSemDone_EhErro_AMinutaPodeEstarTruncada()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Ndjson(Line("só o começo"))));

        var act = () => Collect(Service(handler).ChatStreamAsync(Chat()));

        await act.Should().ThrowAsync<LlmServiceException>().WithMessage("*sem concluir*");
    }

    [Fact]
    public async Task LinhaQueNaoEhJson_VirgaLlmServiceException()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Ndjson("isto não é json")));

        var act = () => Collect(Service(handler).ChatStreamAsync(Chat()));

        await act.Should().ThrowAsync<LlmServiceException>().WithMessage("*Linha inválida*");
    }

    [Fact]
    public async Task Embed_PostaNoApiEmbedComOModeloBgeM3EDevolveOsVetores()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"embeddings\":[[0.1,0.2],[0.3,0.4]]}", Encoding.UTF8, "application/json"),
        }));

        var vectors = await Service(handler).EmbedAsync(["um", "dois"]);

        vectors.Should().HaveCount(2);
        vectors[1].Should().Equal(0.3f, 0.4f);
        var (request, body) = handler.Calls.Single();
        request.RequestUri!.AbsolutePath.Should().Be("/api/embed");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("model").GetString().Should().Be("bge-m3");
        json.RootElement.GetProperty("input").EnumerateArray().Select(e => e.GetString()).Should().Equal("um", "dois");
    }

    [Fact]
    public async Task Embed_ComQuantidadeDeVetoresDiferenteDaPedida_EhErro()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"embeddings\":[[0.1]]}", Encoding.UTF8, "application/json"),
        }));

        var act = () => Service(handler).EmbedAsync(["um", "dois"]);

        await act.Should().ThrowAsync<LlmServiceException>();
    }

    [Fact]
    public async Task Embed_ComHttp503_VirgaLlmServiceException()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var act = () => Service(handler).EmbedAsync(["um"]);

        await act.Should().ThrowAsync<LlmServiceException>().WithMessage("*503*");
    }

    // ------------------------------------------------------------ saúde, modelos e endereço dinâmico

    [Fact]
    public async Task Saude_EhVerdadeiraQuandoApiTagsResponde200()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"models\":[]}", Encoding.UTF8, "application/json"),
        }));

        var saudavel = await Service(handler).VerificarSaudeAsync();

        saudavel.Should().BeTrue();
        var (request, _) = handler.Calls.Single();
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.Should().Be("/api/tags");
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Saude_EhFalsaComErroHttp_SemLancar(HttpStatusCode codigo)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(codigo)));

        (await Service(handler).VerificarSaudeAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Saude_EhFalsaQuandoNaoConecta_ENaoLanca()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("connection refused"));

        (await Service(handler).VerificarSaudeAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Saude_PropagaCancelamentoDoChamador()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);

            return Ndjson();
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = () => Service(handler).VerificarSaudeAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ListarModelos_DevolveOsNomesInstalados()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"models\":[{\"name\":\"qwen3:8b\",\"size\":123},{\"name\":\"bge-m3:latest\"}]}", Encoding.UTF8, "application/json"),
        }));

        var modelos = await Service(handler).ListarModelosAsync();

        modelos.Should().Equal("qwen3:8b", "bge-m3:latest");
    }

    [Fact]
    public async Task ListarModelos_ComCorpoInesperado_EhErro()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"outra\":1}", Encoding.UTF8, "application/json"),
        }));

        var act = () => Service(handler).ListarModelosAsync();

        await act.Should().ThrowAsync<LlmServiceException>();
    }

    [Fact]
    public async Task OEnderecoVemDoEndpointACadaChamada_EPodeMudarComAGpuReiniciando()
    {
        var options = new OllamaOptions { BaseUrl = "https://ia1.exemplo.com.br", TimeoutSegundos = 30 };
        var endpoint = new OllamaEndpoint(Options.Create(options));
        var handler = new StubHandler((_, _) => Task.FromResult(Ndjson(Line(string.Empty, done: true))));
        var service = new OllamaService(new HttpClient(handler), endpoint, Options.Create(options), NullLogger<OllamaService>.Instance);

        await Collect(service.ChatStreamAsync(Chat()));
        endpoint.Set(new Uri("https://ia2.exemplo.com.br/"));
        await Collect(service.ChatStreamAsync(Chat()));

        handler.Calls[0].Request.RequestUri!.Host.Should().Be("ia1.exemplo.com.br");
        handler.Calls[1].Request.RequestUri!.Host.Should().Be("ia2.exemplo.com.br");
    }

    [Fact]
    public void Endpoint_RecusaHttpParaHostPublico_ComoOGuardDeTransporte()
    {
        var endpoint = new OllamaEndpoint(Options.Create(new OllamaOptions { BaseUrl = "https://ia.exemplo.com.br" }));

        var act = () => endpoint.Set(new Uri("http://203.0.113.9:41234/"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*AllowInsecureHttp*");
        endpoint.BaseAddress.Host.Should().Be("ia.exemplo.com.br");
    }

    [Fact]
    public void Endpoint_ComUrlDePlaceholderNaoSubstituida_FalhaAltoNoPrimeiroUso()
    {
        var endpoint = new OllamaEndpoint(Options.Create(new OllamaOptions { BaseUrl = "https://IA_HOST_COM_TLS:PORTA" }));

        var act = () => endpoint.BaseAddress;

        act.Should().Throw<InvalidOperationException>().WithMessage("*inválida*");
    }

    [Fact]
    public async Task EnderecoInvalido_NaoViraEspera_ViraErroDeConfiguracao()
    {
        var options = new OllamaOptions { BaseUrl = "https://IA_HOST_COM_TLS:PORTA" };
        var service = new OllamaService(
            new HttpClient(new StubHandler((_, _) => Task.FromResult(Ndjson()))),
            new OllamaEndpoint(Options.Create(options)),
            Options.Create(options),
            NullLogger<OllamaService>.Instance);

        var act = () => service.VerificarSaudeAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void NomeDoModelo_VemDaConfiguracao()
    {
        Service(new StubHandler((_, _) => Task.FromResult(Ndjson())), o => o.Modelo = "llama3.1:8b")
            .ModelName.Should().Be("llama3.1:8b");
    }
}
