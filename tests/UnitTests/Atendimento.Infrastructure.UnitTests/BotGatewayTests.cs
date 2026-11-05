using System.Net;
using System.Text;
using System.Text.Json;
using Advocacia.Modules.Atendimentos.Infrastructure.Bot;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Atendimentos.Infrastructure.UnitTests;

/// <summary>A saída Core → Bot: o contrato de <c>POST /api/v1/bot/send</c>, nos dois sentidos.</summary>
public class BotGatewayTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Atendimento = Guid.NewGuid();
    private static readonly Guid Mensagem = Guid.NewGuid();

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Url, string? ApiKey, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.RequestUri!.ToString(),
                request.Headers.TryGetValues("X-Api-Key", out var values) ? values.Single() : null,
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            return respond(request);
        }
    }

    private sealed class CapturingLogger : ILogger<BotGateway>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception) + exception);
    }

    private static BotGateway Build(Handler handler, BotGatewayOptions? options = null, ILogger<BotGateway>? logger = null) =>
        new(new HttpClient(handler), Options.Create(options ?? new BotGatewayOptions { BaseUrl = "https://bot.interno/", ApiKey = "chave-do-core" }),
            logger ?? NullLogger<BotGateway>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task PostsTheMessageToTheBot_WithTheCoresKey_AndReturnsTheWamid()
    {
        var handler = new Handler(_ => Json(HttpStatusCode.OK, """{"externalId":"wamid.ABC"}"""));

        var result = await Build(handler).SendTextAsync(Tenant, Atendimento, Mensagem, "5585999998888", "Bom dia", default);

        result.Value.Should().Be("wamid.ABC");
        var request = handler.Requests.Single();
        request.Url.Should().Be("https://bot.interno/api/v1/bot/send");
        request.ApiKey.Should().Be("chave-do-core");
        using var body = JsonDocument.Parse(request.Body);
        body.RootElement.GetProperty("tenantId").GetGuid().Should().Be(Tenant);
        body.RootElement.GetProperty("atendimentoId").GetGuid().Should().Be(Atendimento);
        body.RootElement.GetProperty("messageId").GetGuid().Should().Be(Mensagem, "é a chave de idempotência do Bot");
        body.RootElement.GetProperty("to").GetString().Should().Be("5585999998888");
        body.RootElement.GetProperty("body").GetString().Should().Be("Bom dia");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ANonSuccessResponse_IsAFailure(HttpStatusCode status)
    {
        var result = await Build(new Handler(_ => new HttpResponseMessage(status)))
            .SendTextAsync(Tenant, Atendimento, Mensagem, "5585999998888", "x", default);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ABotThatIsDown_IsAFailure_NotAnException()
    {
        var result = await Build(new Handler(_ => throw new HttpRequestException("recusou")))
            .SendTextAsync(Tenant, Atendimento, Mensagem, "5585999998888", "x", default);

        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "chave")]
    [InlineData("https://bot", null)]
    [InlineData("", "chave")]
    [InlineData("https://bot", "")]
    public async Task WithoutBotConfiguration_NothingIsSent(string? baseUrl, string? apiKey)
    {
        var handler = new Handler(_ => Json(HttpStatusCode.OK, """{"externalId":"x"}"""));

        var result = await Build(handler, new BotGatewayOptions { BaseUrl = baseUrl, ApiKey = apiKey })
            .SendTextAsync(Tenant, Atendimento, Mensagem, "5585999998888", "x", default);

        result.IsFailure.Should().BeTrue();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task NeverLogsThePhoneNumberNorTheMessageText()
    {
        var logger = new CapturingLogger();
        var failing = new Handler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var down = new Handler(_ => throw new HttpRequestException("recusou"));

        await Build(failing, logger: logger).SendTextAsync(Tenant, Atendimento, Mensagem, "5585999998888", "segredo de justiça", default);
        await Build(down, logger: logger).SendTextAsync(Tenant, Atendimento, Mensagem, "5585999998888", "segredo de justiça", default);
        await Build(failing, new BotGatewayOptions(), logger).SendTextAsync(Tenant, Atendimento, Mensagem, "5585999998888", "segredo de justiça", default);

        logger.Messages.Should().NotBeEmpty();
        logger.Messages.Should().NotContain(message => message.Contains("5585999998888") || message.Contains("segredo de justiça"));
    }
}
