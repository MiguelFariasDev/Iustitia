using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Npgsql;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// RLS, privilégios e o CHECK de revisão de <c>processo_movimentacoes</c> e <c>notificacoes_cliente</c>
/// (infra/supabase/migrations/021_movimentacoes_notificacoes.sql — ADR-075), verificados como app_user.
/// </summary>
[Collection(PostgresCollection.Name)]
public class MovimentacoesRlsTests(PostgresFixture fixture)
{
    private static async Task<NpgsqlConnection> OpenAsync(string connectionString, Guid? tenant)
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

    private static NpgsqlCommand InsertMovimentacao(NpgsqlConnection connection, Guid tenant, Guid processo, string externalId)
    {
        var command = new NpgsqlCommand(
            "INSERT INTO processo_movimentacoes (id, tenant_id, processo_id, external_id, fonte, nome, possui_documento, capturada_em, created_at) " +
            "VALUES (gen_random_uuid(), @tenant, @processo, @externo, 'juscraper', 'Sentença', false, now(), now())", connection);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("processo", processo);
        command.Parameters.AddWithValue("externo", externalId);

        return command;
    }

    private static NpgsqlCommand InsertNotificacao(
        NpgsqlConnection connection, Guid tenant, Guid processo, string status, bool revisada, bool enviada)
    {
        var command = new NpgsqlCommand(
            "INSERT INTO notificacoes_cliente (id, tenant_id, processo_id, cliente_id, status, mensagem, quantidade_andamentos, " +
            "andamentos_desde, revisado_por, revisado_em, enviado_em, created_at) " +
            "VALUES (gen_random_uuid(), @tenant, @processo, gen_random_uuid(), @status, 'x', 1, now(), @revisor, @revisado, @enviado, now())",
            connection);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("processo", processo);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("revisor", revisada ? Guid.NewGuid() : DBNull.Value);
        command.Parameters.AddWithValue("revisado", revisada ? DateTimeOffset.UtcNow : DBNull.Value);
        command.Parameters.AddWithValue("enviado", enviada ? DateTimeOffset.UtcNow : DBNull.Value);

        return command;
    }

    [Fact]
    public async Task Andamentos_IsoladosPorEscritorio_EDeduplicadosPorProcesso()
    {
        var a = Guid.NewGuid();
        var processo = Guid.NewGuid();
        await using var comoA = await OpenAsync(fixture.AppUserConnectionString, a);
        await using var comoB = await OpenAsync(fixture.AppUserConnectionString, Guid.NewGuid());

        await InsertMovimentacao(comoA, a, processo, "abc").ExecuteNonQueryAsync();
        var duplicado = () => InsertMovimentacao(comoA, a, processo, "abc").ExecuteNonQueryAsync();

        (await duplicado.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        (await new NpgsqlCommand("SELECT count(*) FROM processo_movimentacoes", comoB).ExecuteScalarAsync()).Should().Be(0L);
    }

    [Fact]
    public async Task Andamento_EImutavel_ParaAAplicacao()
    {
        var tenant = Guid.NewGuid();
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, tenant);
        await InsertMovimentacao(connection, tenant, Guid.NewGuid(), "xyz").ExecuteNonQueryAsync();

        var editar = () => new NpgsqlCommand("UPDATE processo_movimentacoes SET nome = 'outro'", connection).ExecuteNonQueryAsync();

        (await editar.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task AvisoEnviadoSemRevisao_ERecusadoPeloBanco()
    {
        var tenant = Guid.NewGuid();
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, tenant);

        var semRevisao = () => InsertNotificacao(connection, tenant, Guid.NewGuid(), "Enviada", revisada: false, enviada: true).ExecuteNonQueryAsync();
        var semEnvio = () => InsertNotificacao(connection, tenant, Guid.NewGuid(), "Enviada", revisada: true, enviada: false).ExecuteNonQueryAsync();

        (await semRevisao.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be("ck_notificacoes_cliente_enviada_revisada");
        (await semEnvio.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be("ck_notificacoes_cliente_enviada_revisada");
        (await InsertNotificacao(connection, tenant, Guid.NewGuid(), "Enviada", revisada: true, enviada: true).ExecuteNonQueryAsync())
            .Should().Be(1);
    }

    [Fact]
    public async Task UmSoRascunhoPendentePorProcesso()
    {
        var tenant = Guid.NewGuid();
        var processo = Guid.NewGuid();
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, tenant);
        await InsertNotificacao(connection, tenant, processo, "PendenteRevisao", false, false).ExecuteNonQueryAsync();

        var segundo = () => InsertNotificacao(connection, tenant, processo, "PendenteRevisao", false, false).ExecuteNonQueryAsync();

        (await segundo.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }
}
