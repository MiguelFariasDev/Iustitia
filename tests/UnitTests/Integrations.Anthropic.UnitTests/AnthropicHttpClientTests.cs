using System.Text.Json;
using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Integrations.Anthropic.Domain.Models;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Advocacia.Modules.Integrations.Anthropic.UnitTests;

/// <summary>
/// O cliente HTTP contra um servidor WireMock real, montado pelo mesmo AddAnthropicIntegration
/// do host — então o pipeline Polly de verdade (retry, breaker, timeout) está no caminho.
/// </summary>
public sealed class AnthropicHttpClientTests : IDisposable
{
    private const string OkBody = """
        {
          "id": "msg_01",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-5-5",
          "stop_reason": "end_turn",
          "content": [
            { "type": "thinking", "thinking": "" },
            { "type": "text", "text": "{\"a\":" },
            { "type": "text", "text": "1}" }
          ],
          "usage": {
            "input_tokens": 1200,
            "output_tokens": 80,
            "cache_creation_input_tokens": 500,
            "cache_read_input_tokens": 700
          }
        }
        """;

    private const string ToolUseBody = """
        {
          "id": "msg_02",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-5-5",
          "stop_reason": "tool_use",
          "content": [
            { "type": "text", "text": "Vou consultar." },
            { "type": "tool_use", "id": "toolu_01", "name": "consultar_processo", "input": { "numero": "123" } }
          ],
          "usage": { "input_tokens": 300, "output_tokens": 40 }
        }
        """;

    private readonly WireMockServer _server = WireMockServer.Start();
    private readonly CapturingLoggerProvider _logs = new();
    private ServiceProvider? _provider;

    public void Dispose()
    {
        _provider?.Dispose();
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public async Task SendMessage_Success_ParsesTextUsageAndSendsTheRequiredHeaders()
    {
        Reply(200, OkBody);
        var client = CreateClient();

        var result = await client.SendMessageAsync("pergunta", "instruções", new AnthropicOptions());

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("msg_01");
        result.Value.Content.Should().Be("{\"a\":1}", "blocos de thinking não entram no texto");
        result.Value.Model.Should().Be("claude-sonnet-5-5");
        result.Value.StopReason.Should().Be("end_turn");
        result.Value.Usage.Should().Be(new AnthropicUsage(1200, 80, 500, 700));
        result.Value.ToolUses.Should().BeEmpty();

        var request = _server.LogEntries.Single().RequestMessage;
        request.Path.Should().Be("/v1/messages");
        request.Headers!["x-api-key"].Should().Equal("test-key");
        request.Headers["anthropic-version"].Should().Equal("2023-06-01");
        request.Headers["Content-Type"].Single().Should().StartWith("application/json");
    }

    [Fact]
    public async Task SendMessage_SendsTheSystemPromptAsACacheableBlock()
    {
        Reply(200, OkBody);

        await CreateClient().SendMessageAsync("pergunta", "instruções", new AnthropicOptions { MaxTokens = 777 });

        using var body = JsonDocument.Parse(LastBody());
        var root = body.RootElement;
        root.GetProperty("model").GetString().Should().Be("claude-sonnet-5-5");
        root.GetProperty("max_tokens").GetInt32().Should().Be(777);
        root.GetProperty("system")[0].GetProperty("type").GetString().Should().Be("text");
        root.GetProperty("system")[0].GetProperty("text").GetString().Should().Be("instruções");
        root.GetProperty("system")[0].GetProperty("cache_control").GetProperty("type").GetString().Should().Be("ephemeral");
        root.GetProperty("messages")[0].GetProperty("role").GetString().Should().Be("user");
        root.GetProperty("messages")[0].GetProperty("content").GetString().Should().Be("pergunta");
        root.GetProperty("output_config").GetProperty("effort").GetString().Should().Be("medium");

        // Modelos atuais rejeitam sampling não padrão: sem pedido explícito, nada é enviado.
        root.TryGetProperty("temperature", out _).Should().BeFalse();
        root.TryGetProperty("tools", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SendMessage_WithCachingDisabled_OmitsCacheControl()
    {
        Reply(200, OkBody);

        await CreateClient().SendMessageAsync("p", "s", new AnthropicOptions { CacheSystemPrompt = false, Model = "outro-modelo" });

        using var body = JsonDocument.Parse(LastBody());
        body.RootElement.GetProperty("model").GetString().Should().Be("outro-modelo");
        body.RootElement.GetProperty("system")[0].TryGetProperty("cache_control", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SendMessageWithTools_SendsToolsAndParsesAToolUseResponse()
    {
        Reply(200, ToolUseBody);
        var tools = new List<AnthropicTool>
        {
            new("consultar_processo", "Consulta um processo", """{"type":"object","properties":{"numero":{"type":"string"}},"required":["numero"]}"""),
        };

        var result = await CreateClient().SendMessageWithToolsAsync([AnthropicMessage.User("consulte o 123")], tools);

        result.IsSuccess.Should().BeTrue();
        result.Value.StopReason.Should().Be("tool_use");
        result.Value.RequestsToolUse.Should().BeTrue();
        result.Value.Content.Should().Be("Vou consultar.");
        var toolUse = result.Value.ToolUses.Should().ContainSingle().Subject;
        toolUse.Id.Should().Be("toolu_01");
        toolUse.Name.Should().Be("consultar_processo");
        using var input = JsonDocument.Parse(toolUse.InputJson);
        input.RootElement.GetProperty("numero").GetString().Should().Be("123");

        using var body = JsonDocument.Parse(LastBody());
        var sent = body.RootElement.GetProperty("tools")[0];
        sent.GetProperty("name").GetString().Should().Be("consultar_processo");
        sent.GetProperty("input_schema").GetProperty("type").GetString().Should().Be("object");
        body.RootElement.GetProperty("tool_choice").GetProperty("type").GetString().Should().Be("auto");
    }

    [Fact]
    public async Task SendMessageWithTools_InvalidToolSchema_FailsWithoutCallingTheApi()
    {
        var result = await CreateClient().SendMessageWithToolsAsync(
            [AnthropicMessage.User("oi")], [new AnthropicTool("t", "d", "isto não é json")]);

        result.Error.Code.Should().Be("AI_INVALID_REQUEST");
        _server.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task Unauthorized_MapsToAiUnauthorized_WithoutRetry()
    {
        Reply(401, """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""");

        var result = await CreateClient().SendMessageAsync("p", "s", new AnthropicOptions());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AI_UNAUTHORIZED");
        _server.LogEntries.Should().HaveCount(1, "401 não é transitório: repetir só gasta quota");
    }

    [Fact]
    public async Task BadRequest_MapsToAiInvalidRequest_WithoutRetry_AndNeverEchoesTheErrorMessage()
    {
        Reply(400, """{"type":"error","error":{"type":"invalid_request_error","message":"conteudo-sigiloso-do-processo"}}""");

        var result = await CreateClient().SendMessageAsync("p", "s", new AnthropicOptions());

        result.Error.Code.Should().Be("AI_INVALID_REQUEST");
        result.Error.Message.Should().NotContain("conteudo-sigiloso-do-processo");
        _server.LogEntries.Should().HaveCount(1);
        _logs.Text.Should().NotContain("conteudo-sigiloso-do-processo");
        _logs.Text.Should().Contain("invalid_request_error", "o TIPO do erro é seguro e útil para diagnóstico");
    }

    [Fact]
    public async Task RateLimit_IsRetried_AndSucceedsWhenTheApiRecovers()
    {
        _server.Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .InScenario("rate-limit").WhenStateIs("Started").WillSetStateTo("recovered")
            .RespondWith(Response.Create().WithStatusCode(429).WithBody("""{"type":"error","error":{"type":"rate_limit_error","message":"x"}}"""));
        _server.Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .InScenario("rate-limit").WhenStateIs("recovered")
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(OkBody));

        var result = await CreateClient().SendMessageAsync("p", "s", new AnthropicOptions());

        result.IsSuccess.Should().BeTrue();
        _server.LogEntries.Should().HaveCount(2, "uma tentativa com 429 e uma de retry bem-sucedida");
    }

    [Fact]
    public async Task RateLimit_Persistent_ExhaustsTheRetriesAndMapsToAiRateLimited()
    {
        Reply(429, """{"type":"error","error":{"type":"rate_limit_error","message":"x"}}""");

        var result = await CreateClient(maxRetries: 2).SendMessageAsync("p", "s", new AnthropicOptions());

        result.Error.Code.Should().Be("AI_RATE_LIMITED");
        _server.LogEntries.Should().HaveCount(3, "1 tentativa + 2 retries");
    }

    [Fact]
    public async Task Overloaded_529_MapsToAiOverloaded_AfterRetrying()
    {
        Reply(529, """{"type":"error","error":{"type":"overloaded_error","message":"x"}}""");

        var result = await CreateClient(maxRetries: 1).SendMessageAsync("p", "s", new AnthropicOptions());

        result.Error.Code.Should().Be("AI_OVERLOADED");
        _server.LogEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task ServerError_500_MapsToAiUnavailable()
    {
        Reply(500, "{}");

        var result = await CreateClient(maxRetries: 1).SendMessageAsync("p", "s", new AnthropicOptions());

        result.Error.Code.Should().Be("AI_UNAVAILABLE");
    }

    [Fact]
    public async Task SlowResponse_MapsToAiTimeout()
    {
        _server.Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(OkBody).WithDelay(TimeSpan.FromSeconds(4)));

        var result = await CreateClient(maxRetries: 1, timeoutSeconds: 1).SendMessageAsync("p", "s", new AnthropicOptions());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AI_TIMEOUT");
    }

    [Fact]
    public async Task MalformedBody_MapsToAiParseError()
    {
        Reply(200, "isto não é json");

        var result = await CreateClient().SendMessageAsync("p", "s", new AnthropicOptions());

        result.Error.Code.Should().Be("AI_PARSE_ERROR");
    }

    [Fact]
    public async Task EmptyJsonNull_MapsToAiParseError()
    {
        Reply(200, "null");

        var result = await CreateClient().SendMessageAsync("p", "s", new AnthropicOptions());

        result.Error.Code.Should().Be("AI_PARSE_ERROR");
    }

    [Fact]
    public async Task MissingApiKey_FailsWithoutCallingTheApi()
    {
        Reply(200, OkBody);

        var result = await CreateClient(apiKey: "").SendMessageAsync("p", "s", new AnthropicOptions());

        result.Error.Code.Should().Be("AI_UNAUTHORIZED");
        _server.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task NothingSensitive_EverReachesTheLogs()
    {
        // Regra de ouro (OAB/LGPD): nem o prompt, nem o system prompt, nem o texto da resposta.
        Reply(200, OkBody.Replace("{\\\"a\\\":", "TEXTO-DA-RESPOSTA-SIGILOSA"));

        await CreateClient().SendMessageAsync("PROMPT-SIGILOSO-DO-CLIENTE", "SYSTEM-PROMPT-SIGILOSO", new AnthropicOptions());

        _logs.Text.Should().NotBeEmpty("a chamada gera logs de metadados");
        _logs.Text.Should().NotContain("PROMPT-SIGILOSO-DO-CLIENTE");
        _logs.Text.Should().NotContain("SYSTEM-PROMPT-SIGILOSO");
        _logs.Text.Should().NotContain("TEXTO-DA-RESPOSTA-SIGILOSA");
    }

    [Fact]
    public async Task CancelledByTheCaller_Propagates()
    {
        Reply(200, OkBody);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = () => CreateClient().SendMessageAsync("p", "s", new AnthropicOptions(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private void Reply(int status, string body) =>
        _server.Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));

    private string LastBody() => _server.LogEntries.Last().RequestMessage.Body!;

    private IAnthropicClient CreateClient(string apiKey = "test-key", int maxRetries = 3, int timeoutSeconds = 30)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=nao-usado",
                ["Anthropic:BaseUrl"] = _server.Url + "/v1/",
                ["Anthropic:ApiKey"] = apiKey,
                ["Anthropic:MaxRetries"] = maxRetries.ToString(),
                ["Anthropic:RetryBaseDelaySeconds"] = "0",
                ["Anthropic:TimeoutSeconds"] = timeoutSeconds.ToString(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(_logs));
        services.AddAnthropicIntegration(configuration);

        _provider = services.BuildServiceProvider();

        return _provider.GetRequiredService<IAnthropicClient>();
    }
}
