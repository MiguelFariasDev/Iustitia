using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Npgsql;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// As garantias do banco para o módulo Atendimento (infra/supabase/migrations/014_atendimentos.sql),
/// verificadas conectando como <c>app_user</c> — o role da aplicação em produção. O dono das
/// tabelas bypassa RLS, então só esta conexão prova que o isolamento é do BANCO, e não apenas
/// do filtro global do EF Core.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AtendimentoRlsTests(PostgresFixture fixture)
{
    private static string Phone() => "5585" + Random.Shared.NextInt64(900_000_000, 999_999_999);

    private async Task<NpgsqlConnection> OpenAsync(string connectionString, Guid? tenant)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        if (tenant is not null)
        {
            await using var set = connection.CreateCommand();
            set.CommandText = $"SET app.current_tenant = '{tenant}'";
            await set.ExecuteNonQueryAsync();
        }

        return connection;
    }

    private async Task<Guid> SeedAtendimentoAsync(Guid tenant, string? phone = null, string status = "Bot")
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenAsync(fixture.SuperuserConnectionString, null);
        await using var command = new NpgsqlCommand(
            "INSERT INTO atendimentos (id, tenant_id, phone_number, status, last_message_at, created_at) " +
            "VALUES (@id, @tenant, @phone, @status, now(), now())", connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("phone", phone ?? Phone());
        command.Parameters.AddWithValue("status", status);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private async Task SeedMensagemAsync(Guid tenant, Guid atendimento, string? externalId = null)
    {
        await using var connection = await OpenAsync(fixture.SuperuserConnectionString, null);
        await using var command = new NpgsqlCommand(
            "INSERT INTO atendimento_mensagens (id, tenant_id, atendimento_id, direction, sender, body, external_id, delivery, created_at) " +
            "VALUES (@id, @tenant, @atendimento, 'Inbound', 'Client', 'oi', @external, 'Sent', now())", connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("atendimento", atendimento);
        command.Parameters.AddWithValue("external", (object?)externalId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> SeedAuditoriaAsync(Guid tenant, Guid atendimento)
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenAsync(fixture.SuperuserConnectionString, null);
        await using var command = new NpgsqlCommand(
            "INSERT INTO atendimento_auditoria (id, tenant_id, atendimento_id, action, actor_type, occurred_at) " +
            "VALUES (@id, @tenant, @atendimento, 'Started', 'Bot', now())", connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("atendimento", atendimento);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string table)
    {
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table}", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Theory]
    [InlineData("atendimentos")]
    [InlineData("atendimento_mensagens")]
    [InlineData("atendimento_auditoria")]
    public async Task AppUser_OnlySeesTheRowsOfItsOwnTenant(string table)
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var atendimentoA = await SeedAtendimentoAsync(a);
        var atendimentoB = await SeedAtendimentoAsync(b);
        await SeedMensagemAsync(a, atendimentoA);
        await SeedMensagemAsync(b, atendimentoB);
        await SeedAuditoriaAsync(a, atendimentoA);
        await SeedAuditoriaAsync(b, atendimentoB);

        await using var asA = await OpenAsync(fixture.AppUserConnectionString, a);
        await using var command = new NpgsqlCommand($"SELECT count(*) FILTER (WHERE tenant_id = @a), count(*) FILTER (WHERE tenant_id = @b) FROM {table}", asA);
        command.Parameters.AddWithValue("a", a);
        command.Parameters.AddWithValue("b", b);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        reader.GetInt64(0).Should().BeGreaterThan(0);
        reader.GetInt64(1).Should().Be(0, "o escritório A jamais pode ler linhas do escritório B");
    }

    [Fact]
    public async Task AppUser_WithoutATenantInTheSession_SeesNothing()
    {
        await SeedAtendimentoAsync(Guid.NewGuid());

        await using var connection = await OpenAsync(fixture.AppUserConnectionString, null);

        (await CountAsync(connection, "atendimentos")).Should().Be(0, "sem tenant a política falha fechada");
    }

    [Fact]
    public async Task AppUser_CannotInsertARowForAnotherTenant()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        await using var asA = await OpenAsync(fixture.AppUserConnectionString, a);
        await using var command = new NpgsqlCommand(
            "INSERT INTO atendimentos (id, tenant_id, phone_number, status, last_message_at, created_at) " +
            "VALUES (gen_random_uuid(), @other, @phone, 'Bot', now(), now())", asA);
        command.Parameters.AddWithValue("other", b);
        command.Parameters.AddWithValue("phone", Phone());

        var act = async () => await command.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task AppUser_CannotMoveAConversationToAnotherTenant()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var id = await SeedAtendimentoAsync(a);
        await using var asA = await OpenAsync(fixture.AppUserConnectionString, a);
        await using var command = new NpgsqlCommand("UPDATE atendimentos SET tenant_id = @other WHERE id = @id", asA);
        command.Parameters.AddWithValue("other", b);
        command.Parameters.AddWithValue("id", id);

        var act = async () => await command.ExecuteNonQueryAsync();

        // A policy de USING também vale como WITH CHECK no UPDATE: a linha não pode "fugir" de A.
        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task AppUser_CannotAlterOrDeleteTheAuditTrail()
    {
        var tenant = Guid.NewGuid();
        var atendimento = await SeedAtendimentoAsync(tenant);
        var auditoria = await SeedAuditoriaAsync(tenant, atendimento);
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, tenant);

        foreach (var sql in new[]
        {
            "UPDATE atendimento_auditoria SET action = 'Closed' WHERE id = @id",
            "DELETE FROM atendimento_auditoria WHERE id = @id",
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", auditoria);
            var act = async () => await command.ExecuteNonQueryAsync();
            (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501", sql);
        }
    }

    [Theory]
    [InlineData("UPDATE atendimento_mensagens SET body = 'reescrito'")]
    [InlineData("UPDATE atendimento_mensagens SET tenant_id = gen_random_uuid()")]
    [InlineData("UPDATE atendimentos SET phone_number = '5500000000000'")]
    [InlineData("UPDATE atendimentos SET tenant_id = gen_random_uuid()")]
    public async Task AppUser_CannotRewriteImmutableColumns(string sql)
    {
        var tenant = Guid.NewGuid();
        var atendimento = await SeedAtendimentoAsync(tenant);
        await SeedMensagemAsync(tenant, atendimento);
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, tenant);
        await using var command = new NpgsqlCommand(sql, connection);

        var act = async () => await command.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task AppUser_CanUpdateTheColumnsTheApplicationOwns()
    {
        var tenant = Guid.NewGuid();
        var atendimento = await SeedAtendimentoAsync(tenant);
        await SeedMensagemAsync(tenant, atendimento);
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, tenant);

        foreach (var sql in new[]
        {
            "UPDATE atendimentos SET status = 'Human', assigned_user_id = gen_random_uuid(), last_message_at = now(), updated_at = now(), updated_by = 'x', contact_name = 'Maria', client_id = gen_random_uuid()",
            "UPDATE atendimento_mensagens SET delivery = 'Sent', external_id = 'wamid.ok'",
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            (await command.ExecuteNonQueryAsync()).Should().BeGreaterThan(0, sql);
        }
    }

    [Theory]
    [InlineData("DELETE FROM atendimento_mensagens")]
    [InlineData("DELETE FROM atendimentos")]
    public async Task AppUser_CannotDeleteConversationHistory(string sql)
    {
        var tenant = Guid.NewGuid();
        var atendimento = await SeedAtendimentoAsync(tenant);
        await SeedMensagemAsync(tenant, atendimento);
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, tenant);
        await using var command = new NpgsqlCommand(sql, connection);

        var act = async () => await command.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task OnlyOneOpenAtendimentoPerPhoneAndTenant_ButAClosedOneDoesNotCount()
    {
        var tenant = Guid.NewGuid();
        var phone = Phone();
        var first = await SeedAtendimentoAsync(tenant, phone);

        var duplicate = async () => await SeedAtendimentoAsync(tenant, phone, "PendingHuman");
        (await duplicate.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23505");

        await using (var connection = await OpenAsync(fixture.SuperuserConnectionString, null))
        {
            await using var close = new NpgsqlCommand("UPDATE atendimentos SET status = 'Closed' WHERE id = @id", connection);
            close.Parameters.AddWithValue("id", first);
            await close.ExecuteNonQueryAsync();
        }

        var reopened = async () => await SeedAtendimentoAsync(tenant, phone);
        await reopened.Should().NotThrowAsync("encerrado libera o telefone para uma nova conversa");

        var otherTenant = async () => await SeedAtendimentoAsync(Guid.NewGuid(), phone);
        await otherTenant.Should().NotThrowAsync("o mesmo telefone em outro escritório é outra conversa");
    }

    [Fact]
    public async Task AMetaMessageId_CanBeStoredOnlyOncePerTenant()
    {
        var tenant = Guid.NewGuid();
        var atendimento = await SeedAtendimentoAsync(tenant);
        await SeedMensagemAsync(tenant, atendimento, "wamid.unique");

        var duplicate = async () => await SeedMensagemAsync(tenant, atendimento, "wamid.unique");

        (await duplicate.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23505");
        await SeedMensagemAsync(tenant, atendimento);
        await SeedMensagemAsync(tenant, atendimento);
    }

    [Fact]
    public async Task TheBotKeyLookup_WorksWithoutATenant_BecauseTheKeyIsWhatRevealsIt()
    {
        var tenant = Guid.NewGuid();
        var hash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        await using (var seed = await OpenAsync(fixture.SuperuserConnectionString, null))
        {
            await using var insert = new NpgsqlCommand(
                "INSERT INTO bot_api_keys (id, tenant_id, name, prefix, key_hash, created_at) VALUES (gen_random_uuid(), @tenant, 'k', 'adv_bot_x', @hash, now())", seed);
            insert.Parameters.AddWithValue("tenant", tenant);
            insert.Parameters.AddWithValue("hash", hash);
            await insert.ExecuteNonQueryAsync();
        }

        await using var asApp = await OpenAsync(fixture.AppUserConnectionString, null);
        await using var lookup = new NpgsqlCommand("SELECT tenant_id FROM bot_api_keys WHERE key_hash = @hash", asApp);
        lookup.Parameters.AddWithValue("hash", hash);

        (await lookup.ExecuteScalarAsync()).Should().Be(tenant);
    }

    // ---- sessão de verificação de identidade (015_atendimento_consulta.sql) ----

    private async Task<Guid> SeedSessaoAsync(Guid tenant, Guid atendimento)
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenAsync(fixture.SuperuserConnectionString, null);
        await using var command = new NpgsqlCommand(
            "INSERT INTO atendimento_consulta_sessoes (id, tenant_id, atendimento_id, client_id, started_at, expires_at, failed_attempts, process_ids) " +
            "VALUES (@id, @tenant, @atendimento, gen_random_uuid(), now(), now() + interval '10 minutes', 0, '{}')", connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("atendimento", atendimento);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    [Fact]
    public async Task AppUser_OnlySeesTheVerificationSessionsOfItsOwnTenant()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        await SeedSessaoAsync(a, await SeedAtendimentoAsync(a));
        await SeedSessaoAsync(b, await SeedAtendimentoAsync(b));

        await using var asA = await OpenAsync(fixture.AppUserConnectionString, a);
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FILTER (WHERE tenant_id = @a), count(*) FILTER (WHERE tenant_id = @b) FROM atendimento_consulta_sessoes", asA);
        command.Parameters.AddWithValue("a", a);
        command.Parameters.AddWithValue("b", b);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        reader.GetInt64(0).Should().Be(1);
        reader.GetInt64(1).Should().Be(0);
    }

    [Fact]
    public async Task AppUser_CannotInsertAVerificationSessionForAnotherTenant()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var atendimentoOfB = await SeedAtendimentoAsync(b);
        await using var asA = await OpenAsync(fixture.AppUserConnectionString, a);
        await using var command = new NpgsqlCommand(
            "INSERT INTO atendimento_consulta_sessoes (id, tenant_id, atendimento_id, client_id, started_at, expires_at, failed_attempts, process_ids) " +
            "VALUES (gen_random_uuid(), @other, @atendimento, gen_random_uuid(), now(), now(), 0, '{}')", asA);
        command.Parameters.AddWithValue("other", b);
        command.Parameters.AddWithValue("atendimento", atendimentoOfB);

        var act = async () => await command.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    [Theory]
    [InlineData("UPDATE atendimento_consulta_sessoes SET client_id = gen_random_uuid()")]
    [InlineData("UPDATE atendimento_consulta_sessoes SET tenant_id = gen_random_uuid()")]
    [InlineData("UPDATE atendimento_consulta_sessoes SET atendimento_id = gen_random_uuid()")]
    [InlineData("UPDATE atendimento_consulta_sessoes SET started_at = now()")]
    [InlineData("DELETE FROM atendimento_consulta_sessoes")]
    public async Task AppUser_CannotRewriteWhoASessionBelongsTo_NorDeleteIt(string sql)
    {
        var tenant = Guid.NewGuid();
        await SeedSessaoAsync(tenant, await SeedAtendimentoAsync(tenant));
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, tenant);
        await using var command = new NpgsqlCommand(sql, connection);

        var act = async () => await command.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task AppUser_CanUpdateOnlyTheColumnsTheVerificationChanges()
    {
        var tenant = Guid.NewGuid();
        await SeedSessaoAsync(tenant, await SeedAtendimentoAsync(tenant));
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, tenant);
        await using var command = new NpgsqlCommand(
            "UPDATE atendimento_consulta_sessoes SET expires_at = now(), failed_attempts = 2, last_attempt_at = now(), " +
            "verified_at = now(), blocked_until = now(), process_ids = ARRAY[gen_random_uuid()]", connection);

        (await command.ExecuteNonQueryAsync()).Should().Be(1);
    }

    [Fact]
    public async Task TheSessionTableHasNoColumnThatCouldHoldTheDocument()
    {
        await using var connection = await OpenAsync(fixture.SuperuserConnectionString, null);
        await using var command = new NpgsqlCommand(
            "SELECT column_name FROM information_schema.columns WHERE table_name IN ('atendimento_consulta_sessoes', 'atendimento_auditoria')", connection);
        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        columns.Should().NotBeEmpty();
        columns.Should().NotContain(c => c.Contains("doc", StringComparison.OrdinalIgnoreCase) || c.Contains("cpf") || c.Contains("cnpj"));
    }
}
