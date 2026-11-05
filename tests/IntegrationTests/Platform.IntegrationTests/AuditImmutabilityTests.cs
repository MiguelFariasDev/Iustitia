using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Npgsql;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Imutabilidade e retenção de audit_logs contra um Postgres REAL (014_audit_immutability.sql).
/// A conexão relevante é a de app_user — a mesma que a aplicação usa em produção — e a do dono
/// das tabelas, para provar que até ele é barrado pelo trigger.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AuditImmutabilityTests(PostgresFixture fixture)
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private async Task<NpgsqlConnection> OpenAsync(string connectionString, Guid? tenant = null)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        if (tenant is not null)
        {
            await using var set = connection.CreateCommand();
            set.CommandText = $"SELECT set_config('app.current_tenant', '{tenant}', false)";
            await set.ExecuteScalarAsync();
        }

        return connection;
    }

    private static async Task<Guid> InsertAsync(NpgsqlConnection connection, Guid tenant, DateTimeOffset createdAt)
    {
        var id = Guid.NewGuid();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO audit_logs (id, tenant_id, user_id, action, entity_type, entity_id, ip, user_agent, created_at)
            VALUES (@id, @tenant, @user, 'Created', 'Cliente', @entity, '203.0.113.7', 'xunit', @created)
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("user", Guid.NewGuid());
        command.Parameters.AddWithValue("entity", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("created", createdAt);
        await command.ExecuteNonQueryAsync();

        return id;
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql, params (string, object)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task AppUser_CanInsertAndSelect_ItsOwnTenantsAuditLogs()
    {
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, TenantA);

        var id = await InsertAsync(connection, TenantA, DateTimeOffset.UtcNow);

        (await ScalarAsync(connection, "SELECT count(*) FROM audit_logs WHERE id = @id", ("id", id))).Should().Be(1);
    }

    [Fact]
    public async Task AppUser_CannotInsertForAnotherTenant()
    {
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, TenantA);

        var act = () => InsertAsync(connection, TenantB, DateTimeOffset.UtcNow);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task AppUser_CannotUpdateAnAuditLog()
    {
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, TenantA);
        var id = await InsertAsync(connection, TenantA, DateTimeOffset.UtcNow);

        var act = () => ScalarAsync(connection, "UPDATE audit_logs SET action = 'Deleted' WHERE id = @id", ("id", id));

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task AppUser_CannotDeleteOrTruncate_EvenWhenTheRowIsOlderThanFiveYears()
    {
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, TenantA);
        var id = await InsertAsync(connection, TenantA, DateTimeOffset.UtcNow.AddYears(-6));

        var delete = () => ScalarAsync(connection, "DELETE FROM audit_logs WHERE id = @id", ("id", id));
        var truncate = () => ScalarAsync(connection, "TRUNCATE audit_logs");

        (await delete.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
        (await truncate.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task AppUser_CannotSelfGrantTheMaintenanceFlagToDelete()
    {
        // A flag que libera o DELETE no trigger é uma GUC que qualquer sessão pode setar; o que
        // impede o abuso é a falta do privilégio DELETE (camada 1), não o segredo da flag.
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, TenantA);
        var id = await InsertAsync(connection, TenantA, DateTimeOffset.UtcNow.AddYears(-6));
        await ScalarAsync(connection, "SELECT 1 FROM set_config('app.audit_maintenance', 'on', false)");

        var act = () => ScalarAsync(connection, "DELETE FROM audit_logs WHERE id = @id", ("id", id));

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task Owner_IsAlsoBlockedByTheTrigger_FromUpdateDeleteAndTruncate()
    {
        await using var connection = await OpenAsync(fixture.SuperuserConnectionString);
        var id = await InsertAsync(connection, TenantA, DateTimeOffset.UtcNow);

        foreach (var sql in new[]
        {
            "UPDATE audit_logs SET action = 'Deleted' WHERE id = @id",
            "DELETE FROM audit_logs WHERE id = @id",
            "TRUNCATE audit_logs",
        })
        {
            var act = () => ScalarAsync(connection, sql, ("id", id));
            (await act.Should().ThrowAsync<PostgresException>(sql)).Which.Message.Should().Contain("imutável");
        }

        (await ScalarAsync(connection, "SELECT count(*) FROM audit_logs WHERE id = @id", ("id", id))).Should().Be(1);
    }

    [Fact]
    public async Task Owner_CannotDeleteRecentRows_EvenWithTheMaintenanceFlag()
    {
        await using var connection = await OpenAsync(fixture.SuperuserConnectionString);
        var id = await InsertAsync(connection, TenantA, DateTimeOffset.UtcNow.AddYears(-2));
        await ScalarAsync(connection, "SELECT 1 FROM set_config('app.audit_maintenance', 'on', false)");

        // 2 anos de idade, sem manifesto de arquivamento: nem o dono com a flag apaga.
        var act = () => ScalarAsync(connection, "DELETE FROM audit_logs WHERE id = @id", ("id", id));

        await act.Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task PurgeExpired_RemovesOnlyRowsOlderThanFiveYears()
    {
        var tenant = Guid.NewGuid();
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, tenant);
        var expired = await InsertAsync(connection, tenant, DateTimeOffset.UtcNow.AddYears(-5).AddDays(-2));
        var almost = await InsertAsync(connection, tenant, DateTimeOffset.UtcNow.AddYears(-5).AddDays(2));
        var recent = await InsertAsync(connection, tenant, DateTimeOffset.UtcNow.AddDays(-1));

        var purged = await ScalarAsync(connection, "SELECT audit_purge_expired()");

        purged.Should().BeGreaterThanOrEqualTo(1);
        (await ScalarAsync(connection, "SELECT count(*) FROM audit_logs WHERE id = @id", ("id", expired))).Should().Be(0);
        (await ScalarAsync(connection, "SELECT count(*) FROM audit_logs WHERE id = @id", ("id", almost))).Should().Be(1, "dentro dos 5 anos de retenção legal");
        (await ScalarAsync(connection, "SELECT count(*) FROM audit_logs WHERE id = @id", ("id", recent))).Should().Be(1);
    }

    [Fact]
    public async Task Archive_MovesOnlyFullMonthsOlderThanOneYear_AndOnlyWhenTheCountMatches()
    {
        var tenant = Guid.NewGuid();
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, tenant);
        var old = new DateTimeOffset(DateTimeOffset.UtcNow.AddMonths(-20).Year, DateTimeOffset.UtcNow.AddMonths(-20).Month, 10, 12, 0, 0, TimeSpan.Zero);
        var month = DateOnly.FromDateTime(new DateTime(old.Year, old.Month, 1));
        await InsertAsync(connection, tenant, old);
        await InsertAsync(connection, tenant, old.AddHours(1));
        var recent = await InsertAsync(connection, tenant, DateTimeOffset.UtcNow.AddMonths(-2));

        // O candidato aparece com a contagem certa; o mês recente não é candidato.
        await using (var list = connection.CreateCommand())
        {
            list.CommandText = "SELECT period_month, row_count FROM audit_archive_candidates(100) WHERE tenant_id = @t";
            list.Parameters.AddWithValue("t", tenant);
            await using var reader = await list.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();
            DateOnly.FromDateTime(reader.GetDateTime(0)).Should().Be(month);
            reader.GetInt64(1).Should().Be(2);
            (await reader.ReadAsync()).Should().BeFalse();
        }

        // Contagem errada: o banco recusa e NADA é apagado.
        var wrongCount = () => ScalarAsync(
            connection, "SELECT audit_archive_commit(@t, @m, 3, 'abc', 'https://cold/x')", ("t", tenant), ("m", month));
        (await wrongCount.Should().ThrowAsync<PostgresException>()).Which.Message.Should().Contain("divergente");
        (await ScalarAsync(connection, "SELECT count(*) FROM audit_logs WHERE tenant_id = @t", ("t", tenant))).Should().Be(3);

        // Contagem certa: arquiva (manifesto + remoção) na mesma transação.
        var deleted = await ScalarAsync(
            connection, "SELECT audit_archive_commit(@t, @m, 2, 'abc', 'https://cold/x')", ("t", tenant), ("m", month));
        deleted.Should().Be(2);
        (await ScalarAsync(connection, "SELECT count(*) FROM audit_logs WHERE tenant_id = @t", ("t", tenant))).Should().Be(1);
        (await ScalarAsync(connection, "SELECT count(*) FROM audit_logs WHERE id = @id", ("id", recent))).Should().Be(1);

        await using var owner = await OpenAsync(fixture.SuperuserConnectionString);
        (await ScalarAsync(owner, "SELECT count(*) FROM audit_log_archives WHERE tenant_id = @t AND row_count = 2", ("t", tenant))).Should().Be(1);
    }

    [Fact]
    public async Task Archive_RefusesMonthsYoungerThanOneYear()
    {
        var tenant = Guid.NewGuid();
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, tenant);
        var month = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3));
        month = new DateOnly(month.Year, month.Month, 1);
        await InsertAsync(connection, tenant, DateTimeOffset.UtcNow.AddMonths(-3));

        var act = () => ScalarAsync(
            connection, "SELECT audit_archive_commit(@t, @m, 1, 'abc', 'https://cold/x')", ("t", tenant), ("m", month));

        (await act.Should().ThrowAsync<PostgresException>()).Which.Message.Should().Contain("menos de 1 ano");
    }

    [Fact]
    public async Task ArchiveManifest_IsNotReachableByAppUser_AndIsImmutableForTheOwner()
    {
        await using var app = await OpenAsync(fixture.AppUserConnectionString, TenantA);
        var read = () => ScalarAsync(app, "SELECT count(*) FROM audit_log_archives");
        (await read.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");

        await using var owner = await OpenAsync(fixture.SuperuserConnectionString);
        await ScalarAsync(owner, "INSERT INTO audit_log_archives (tenant_id, period_month, row_count, sha256, blob_uri) VALUES (@t, '2020-01-01', 1, 'h', 'u')", ("t", Guid.NewGuid()));
        var mutate = () => ScalarAsync(owner, "UPDATE audit_log_archives SET row_count = 99");
        (await mutate.Should().ThrowAsync<PostgresException>()).Which.Message.Should().Contain("imutável");
    }

    [Fact]
    public async Task AppUser_DoesNotSeeAnotherTenantsAuditLogs()
    {
        await using var a = await OpenAsync(fixture.AppUserConnectionString, TenantA);
        var id = await InsertAsync(a, TenantA, DateTimeOffset.UtcNow);

        await using var b = await OpenAsync(fixture.AppUserConnectionString, TenantB);

        (await ScalarAsync(b, "SELECT count(*) FROM audit_logs WHERE id = @id", ("id", id))).Should().Be(0);
    }
}
