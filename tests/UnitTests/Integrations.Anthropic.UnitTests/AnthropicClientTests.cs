using System.Net;
using System.Text;
using System.Text.Json;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Integrations.Anthropic.UnitTests;

public class AnthropicClientTests
{
    private const string OkBody = """
        {
          "id": "msg_1",
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

    private static readonly AnthropicRequest Request =
        new("claude-sonnet-5-5", "system prompt", "user prompt", 4096);

    [Fact]
    public async Task SendAsync_Success_ConcatenatesTextBlocksAndMapsUsage()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OkBody);
        var client = CreateClient(handler);

        var result = await client.SendAsync(Request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Be("{\"a\":1}", "blocos de thinking não entram no texto");
        result.Value.Model.Should().Be("claude-sonnet-5-5");
        result.Value.StopReason.Should().Be("end_turn");
        result.Value.RequestId.Should().Be("req_abc");
        result.Value.Usage.Should().Be(new AnthropicUsage(1200, 80, 500, 700));
    }

    [Fact]
    public async Task SendAsync_SendsApiKeyVersionCacheControlAndEffort()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OkBody);
        var client = CreateClient(handler);

        await client.SendAsync(Request);

        handler.LastRequest!.Headers.GetValues("x-api-key").Should().Equal("test-key");
        handler.LastRequest.Headers.GetValues("anthropic-version").Should().Equal("2023-06-01");
        handler.LastRequest.RequestUri!.AbsolutePath.Should().EndWith("/v1/messages");

        using var body = JsonDocument.Parse(handler.LastBody!);
        var root = body.RootElement;
        root.GetProperty("model").GetString().Should().Be("claude-sonnet-5-5");
        root.GetProperty("max_tokens").GetInt32().Should().Be(4096);
        root.GetProperty("system")[0].GetProperty("text").GetString().Should().Be("system prompt");
        root.GetProperty("system")[0].GetProperty("cache_control").GetProperty("type").GetString().Should().Be("ephemeral");
        root.GetProperty("messages")[0].GetProperty("role").GetString().Should().Be("user");
        root.GetProperty("output_config").GetProperty("effort").GetString().Should().Be("medium");

        // Modelos atuais rejeitam sampling não padrão: nada disso pode ser enviado.
        root.TryGetProperty("temperature", out _).Should().BeFalse();
        root.TryGetProperty("top_p", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_WithoutApiKey_FailsWithoutCallingTheApi()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OkBody);
        var client = CreateClient(handler, apiKey: "");

        var result = await client.SendAsync(Request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INTEGRATION_AUTH_FAILED");
        handler.LastRequest.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "INTEGRATION_AUTH_FAILED")]
    [InlineData(HttpStatusCode.Forbidden, "INTEGRATION_AUTH_FAILED")]
    [InlineData(HttpStatusCode.TooManyRequests, "INTEGRATION_RATE_LIMITED")]
    [InlineData(HttpStatusCode.GatewayTimeout, "INTEGRATION_TIMEOUT")]
    [InlineData(HttpStatusCode.InternalServerError, "INTEGRATION_UNAVAILABLE")]
    [InlineData((HttpStatusCode)529, "INTEGRATION_UNAVAILABLE")]
    [InlineData(HttpStatusCode.BadRequest, "INTEGRATION_INVALID_RESPONSE")]
    public async Task SendAsync_HttpErrors_MapToCatalogCodes(HttpStatusCode status, string expectedCode)
    {
        var client = CreateClient(new StubHandler(status, "{\"error\":{\"type\":\"x\",\"message\":\"conteudo-sigiloso\"}}"));

        var result = await client.SendAsync(Request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedCode);
        result.Error.Message.Should().NotContain("conteudo-sigiloso");
    }

    [Fact]
    public async Task SendAsync_MalformedBody_FailsAsInvalidResponse()
    {
        var client = CreateClient(new StubHandler(HttpStatusCode.OK, "isto nao e json"));

        var result = await client.SendAsync(Request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INTEGRATION_INVALID_RESPONSE");
    }

    [Fact]
    public async Task SendAsync_NetworkFailure_FailsAsUnavailable()
    {
        var client = CreateClient(new StubHandler(throws: new HttpRequestException("boom")));

        var result = await client.SendAsync(Request);

        result.Error.Code.Should().Be("INTEGRATION_UNAVAILABLE");
    }

    private static AnthropicClient CreateClient(StubHandler handler, string apiKey = "test-key")
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.test/") };
        var options = Options.Create(new AnthropicOptions { ApiKey = apiKey, Effort = "medium" });

        return new AnthropicClient(httpClient, options, NullLogger<AnthropicClient>.Instance);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly Exception? _throws;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public StubHandler(Exception throws)
        {
            _throws = throws;
            _body = string.Empty;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            if (_throws is not null)
            {
                throw _throws;
            }

            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
            response.Headers.Add("request-id", "req_abc");

            return response;
        }
    }
}
