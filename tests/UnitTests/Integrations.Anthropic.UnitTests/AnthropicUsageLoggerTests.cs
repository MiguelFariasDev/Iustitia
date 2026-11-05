using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Integrations.Anthropic.Domain.Models;
using Advocacia.Modules.Integrations.Anthropic.Domain.Usage;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Persistence;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Usage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Integrations.Anthropic.UnitTests;

public sealed class AnthropicUsageLoggerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private const string Model = "modelo-teste";

    private readonly AnthropicDbContext _dbContext;
    private readonly CapturingLoggerProvider _logs = new();
    private readonly AnthropicUsageLogger _logger;

    public AnthropicUsageLoggerTests()
    {
        var options = new DbContextOptionsBuilder<AnthropicDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AnthropicDbContext(options, new AsyncLocalTenantContext());

        var settings = Options.Create(new AnthropicSettings
        {
            Pricing =
            {
                [Model] = new ModelPricing
                {
                    InputPerMillion = 2m,
                    OutputPerMillion = 10m,
                    CacheWritePerMillion = 2.5m,
                    CacheReadPerMillion = 0.2m,
                },
            },
        });

        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(_logs));

        _logger = new AnthropicUsageLogger(
            _dbContext,
            new AiCostCalculator(settings, loggerFactory.CreateLogger<AiCostCalculator>()),
            new FixedClock(),
            loggerFactory.CreateLogger<AnthropicUsageLogger>());
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task LogUsageAsync_PersistsTokensLatencyAndTheComputedCost()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // 1000*2 + 100*10 + 200*2,5 + 3000*0,2 = 4.100 / 1.000.000 = 0,0041
        await _logger.LogUsageAsync(tenantId, userId, Model, new AnthropicUsage(1000, 100, 200, 3000), latencyMs: 1234);

        var row = await _dbContext.AiUsageLogs.IgnoreQueryFilters().SingleAsync();
        row.TenantId.Should().Be(tenantId);
        row.UserId.Should().Be(userId);
        row.Model.Should().Be(Model);
        row.InputTokens.Should().Be(1000);
        row.OutputTokens.Should().Be(100);
        row.CacheCreationInputTokens.Should().Be(200);
        row.CacheReadInputTokens.Should().Be(3000);
        row.CostUsd.Should().Be(0.0041m);
        row.LatencyMs.Should().Be(1234);
        row.Success.Should().BeTrue();
        row.ErrorCode.Should().BeNull();
        row.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task LogUsageAsync_WithEmptyUser_StoresNoUser()
    {
        await _logger.LogUsageAsync(Guid.NewGuid(), Guid.Empty, Model, AnthropicUsage.None, 10);

        (await _dbContext.AiUsageLogs.IgnoreQueryFilters().SingleAsync()).UserId.Should().BeNull();
    }

    [Fact]
    public async Task LogAsync_FailedCall_StoresTheErrorCodeAndTheBusinessReference()
    {
        var publicationId = Guid.NewGuid();

        await _logger.LogAsync(new AnthropicUsageLogEntry(
            Guid.NewGuid(), null, Model, new AnthropicUsage(1500, 40, 0, 0), 900,
            Success: false, ErrorCode: "AI_INVALID_RESPONSE", Purpose: "classification",
            PromptVersion: "1.0.0", ReferenceId: publicationId, RequestId: "req_9"));

        var row = await _dbContext.AiUsageLogs.IgnoreQueryFilters().SingleAsync();
        row.Success.Should().BeFalse();
        row.ErrorCode.Should().Be("AI_INVALID_RESPONSE");
        row.Purpose.Should().Be("classification");
        row.PromptVersion.Should().Be("1.0.0");
        row.ReferenceId.Should().Be(publicationId);
        row.RequestId.Should().Be("req_9");
        row.CostUsd.Should().BeGreaterThan(0m, "a resposta inválida foi cobrada");
    }

    [Fact]
    public async Task LogAsync_WithoutTenant_Throws()
    {
        var act = () => _logger.LogAsync(new AnthropicUsageLogEntry(Guid.Empty, null, Model, AnthropicUsage.None, 1, true));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LogAsync_VeryLongLatency_IsClampedToAnInt()
    {
        await _logger.LogAsync(new AnthropicUsageLogEntry(Guid.NewGuid(), null, Model, AnthropicUsage.None, long.MaxValue, true));

        (await _dbContext.AiUsageLogs.IgnoreQueryFilters().SingleAsync()).LatencyMs.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task Logging_NeverWritesAnyTextPayloadToTheSystemLog()
    {
        // O tipo de entrada não tem onde carregar prompt/resposta; o log de sistema só pode ter
        // metadados. Confere o que de fato foi logado.
        await _logger.LogAsync(new AnthropicUsageLogEntry(
            Guid.NewGuid(), null, Model, new AnthropicUsage(10, 5, 0, 0), 20, true, Purpose: "classification"));

        _logs.Text.Should().Contain(Model);
        _logs.Text.Should().NotContainAny("prompt", "Prompt", "conteúdo");
    }

    [Fact]
    public void TheAuditRecordAndItsEntry_HaveNoPropertyThatCouldHoldMessageContent()
    {
        // Barreira estrutural da regra de ouro (OAB/LGPD): ninguém consegue gravar o conteúdo
        // porque não há campo para ele. Se alguém adicionar um, este teste obriga a discutir.
        var forbidden = new[] { "prompt", "content", "conteudo", "text", "texto", "message", "response", "resposta", "body", "system" };

        var names = typeof(AiUsageLog).GetProperties().Select(property => property.Name)
            .Concat(typeof(AnthropicUsageLogEntry).GetProperties().Select(property => property.Name))
            .Where(name => name != nameof(AiUsageLog.PromptVersion));

        foreach (var name in names)
        {
            forbidden.Should().NotContain(
                word => name.Contains(word, StringComparison.OrdinalIgnoreCase), $"'{name}' sugere conteúdo de mensagem");
        }
    }

    [Fact]
    public async Task UsageQuery_CountsFailuresByReference_ForThePurposeAndErrorCodeOnly()
    {
        var tenantId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        async Task Add(Guid reference, string purpose, bool success, string? error) =>
            await _logger.LogAsync(new AnthropicUsageLogEntry(
                tenantId, null, Model, AnthropicUsage.None, 1, success, error, purpose, ReferenceId: reference));

        await Add(first, "classification", false, "AI_INVALID_RESPONSE");
        await Add(first, "classification", false, "AI_INVALID_RESPONSE");
        await Add(first, "classification", true, null);
        await Add(first, "classification", false, "AI_TIMEOUT");
        await Add(first, "deadline", false, "AI_INVALID_RESPONSE");
        await Add(second, "classification", false, "AI_INVALID_RESPONSE");

        var counts = await new AnthropicUsageQuery(_dbContext).CountFailuresByReferenceAsync(
            tenantId, "classification", "AI_INVALID_RESPONSE", [first, second, Guid.NewGuid()]);

        counts[first].Should().Be(2);
        counts[second].Should().Be(1);
        counts.Should().HaveCount(2);
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => Now;

        public DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime);
    }
}
