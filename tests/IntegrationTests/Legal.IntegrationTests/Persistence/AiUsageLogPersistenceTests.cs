using Advocacia.Modules.Integrations.Anthropic.Domain.Models;
using Advocacia.Modules.Integrations.Anthropic.Domain.Usage;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Persistence;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Usage;
using Advocacia.Modules.Legal.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Advocacia.Modules.Legal.IntegrationTests.Persistence;

/// <summary>
/// ai_usage_logs (módulo Anthropic — ver 015_ai_usage_logs.sql e ADR-069). Reaproveita o
/// container desta collection, que já migra o AnthropicDbContext. Os testes de RLS e de
/// permissão conectam como app_user: o dono das tabelas bypassa o RLS.
/// </summary>
[Collection(LegalPostgresCollection.Name)]
public class AiUsageLogPersistenceTests(LegalPostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RlsIsEnabled()
    {
        await using var connection = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT relrowsecurity FROM pg_class WHERE relname = 'ai_usage_logs'";

        ((bool?)await command.ExecuteScalarAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Row_RoundTrips_WithTheCostPrecisionIntact()
    {
        var tenantId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        await SeedAsync(tenantId, referenceId, success: true, errorCode: null, cost: 0.002815m);

        await using var context = SuperuserContext(tenantId);
        var row = await context.AiUsageLogs.SingleAsync(log => log.ReferenceId == referenceId);

        row.CostUsd.Should().Be(0.002815m);
        row.Success.Should().BeTrue();
        row.InputTokens.Should().Be(1000);
        row.CacheReadInputTokens.Should().Be(300);
    }

    [Fact]
    public async Task AsAppUser_ATenantNeverSeesAnotherTenantsLogs()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedAsync(tenantA, Guid.NewGuid(), true, null);
        await SeedAsync(tenantB, Guid.NewGuid(), true, null);

        await using var contextA = AppUserContext(tenantA);
        var visible = await contextA.AiUsageLogs.IgnoreQueryFilters().ToListAsync();

        visible.Should().OnlyContain(log => log.TenantId == tenantA);
    }

    [Fact]
    public async Task AsAppUser_CannotInsertForAnotherTenant()
    {
        await using var context = AppUserContext(Guid.NewGuid());
        context.AiUsageLogs.Add(NewLog(Guid.NewGuid(), Guid.NewGuid(), true, null, 0.001m));

        var act = () => context.SaveChangesAsync();

        var exception = await act.Should().ThrowAsync<DbUpdateException>();
        exception.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task AsAppUser_LogsAreAppendOnly()
    {
        var tenantId = Guid.NewGuid();
        await SeedAsync(tenantId, Guid.NewGuid(), true, null);

        await using var connection = new NpgsqlConnection(fixture.AppUserConnectionString);
        await connection.OpenAsync();

        await using (var setTenant = connection.CreateCommand())
        {
            setTenant.CommandText = $"SET app.current_tenant = '{tenantId}'";
            await setTenant.ExecuteNonQueryAsync();
        }

        foreach (var statement in new[] { "UPDATE ai_usage_logs SET cost_usd = 0", "DELETE FROM ai_usage_logs" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;

            var act = () => command.ExecuteNonQueryAsync();

            var exception = await act.Should().ThrowAsync<PostgresException>(statement);
            exception.Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);
        }
    }

    [Fact]
    public async Task Database_RejectsSuccessWithAnErrorCode_AndNegativeTokens()
    {
        var tenantId = Guid.NewGuid();

        foreach (var (success, error, tokens) in new[] { (true, "AI_TIMEOUT", 10), (false, (string?)null, 10), (true, (string?)null, -1) })
        {
            await using var connection = new NpgsqlConnection(fixture.SuperuserConnectionString);
            await connection.OpenAsync();

            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO ai_usage_logs
                  (id, tenant_id, model, input_tokens, output_tokens, cache_creation_input_tokens,
                   cache_read_input_tokens, cost_usd, latency_ms, success, error_code, created_at)
                VALUES (@id, @tenant, 'm', @tokens, 0, 0, 0, 0, 1, @success, @error, now())
                """;
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("tenant", tenantId);
            insert.Parameters.AddWithValue("tokens", tokens);
            insert.Parameters.AddWithValue("success", success);
            insert.Parameters.AddWithValue("error", (object?)error ?? DBNull.Value);

            var act = () => insert.ExecuteNonQueryAsync();

            var exception = await act.Should().ThrowAsync<PostgresException>();
            exception.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        }
    }

    [Fact]
    public async Task UsageQuery_CountsFailuresAgainstTheRealDatabase()
    {
        var tenantId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();
        var other = Guid.NewGuid();
        await SeedAsync(tenantId, publicationId, false, "AI_INVALID_RESPONSE");
        await SeedAsync(tenantId, publicationId, false, "AI_INVALID_RESPONSE");
        await SeedAsync(tenantId, publicationId, true, null);
        await SeedAsync(tenantId, other, false, "AI_TIMEOUT");

        await using var context = SuperuserContext(tenantId);
        var counts = await new AnthropicUsageQuery(context).CountFailuresByReferenceAsync(
            tenantId, "classification", "AI_INVALID_RESPONSE", [publicationId, other]);

        counts.Should().ContainSingle().Which.Should().Be(new KeyValuePair<Guid, int>(publicationId, 2));
    }

    private async Task SeedAsync(Guid tenantId, Guid referenceId, bool success, string? errorCode, decimal cost = 0.001m)
    {
        await using var context = SuperuserContext(tenantId);
        context.AiUsageLogs.Add(NewLog(tenantId, referenceId, success, errorCode, cost));
        await context.SaveChangesAsync();
    }

    private static AiUsageLog NewLog(Guid tenantId, Guid referenceId, bool success, string? errorCode, decimal cost) =>
        AiUsageLog.Create(
            tenantId, null, "claude-sonnet-5-5", "classification", "1.0.0", referenceId,
            1000, 80, 0, 300, cost, 1200, success, errorCode, "req_1", Now);

    private AnthropicDbContext SuperuserContext(Guid tenantId) => Create(fixture.SuperuserConnectionString, tenantId);

    private AnthropicDbContext AppUserContext(Guid tenantId) => Create(fixture.AppUserConnectionString, tenantId);

    private static AnthropicDbContext Create(string connectionString, Guid tenantId)
    {
        var tenantContext = TestLegalDbContextFactory.TenantContextFor(tenantId);
        var options = new DbContextOptionsBuilder<AnthropicDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors.TenantContextInterceptor(tenantContext))
            .Options;

        return new AnthropicDbContext(options, tenantContext);
    }
}
