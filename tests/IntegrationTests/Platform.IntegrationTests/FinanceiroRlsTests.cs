using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Npgsql;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// RLS e constraints de <c>lancamentos</c> (infra/supabase/migrations/019_financeiro.sql), verificados
/// como <c>app_user</c> — o role da aplicação. O dono das tabelas bypassa RLS.
/// </summary>
[Collection(PostgresCollection.Name)]
public class FinanceiroRlsTests(PostgresFixture fixture)
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

    private async Task SeedAsync(Guid tenant, string status = "Pendente", bool comPagoEm = false, decimal valor = 10m)
    {
        await using var connection = await OpenAsync(fixture.SuperuserConnectionString, null);
        await using var command = new NpgsqlCommand(
            "INSERT INTO lancamentos (id, tenant_id, tipo, descricao, valor, vencimento, status, pago_em, created_at, is_deleted) " +
            "VALUES (@id, @tenant, 'Honorario', 'x', @valor, current_date, @status, @pago, now(), false)", connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("valor", valor);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("pago", comPagoEm ? DateTimeOffset.UtcNow : DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task AppUser_SeesOnlyItsOwnTenantsEntries_AndNothingWithoutATenant()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await SeedAsync(a);
        await SeedAsync(b);

        await using var comoA = await OpenAsync(fixture.AppUserConnectionString, a);
        await using var semTenant = await OpenAsync(fixture.AppUserConnectionString, null);

        (await new NpgsqlCommand("SELECT count(*) FROM lancamentos WHERE tenant_id <> current_setting('app.current_tenant')::uuid", comoA).ExecuteScalarAsync())
            .Should().Be(0L);
        (await new NpgsqlCommand("SELECT count(*) FROM lancamentos", comoA).ExecuteScalarAsync()).Should().Be(1L);
        (await new NpgsqlCommand("SELECT count(*) FROM lancamentos", semTenant).ExecuteScalarAsync()).Should().Be(0L);
    }

    [Fact]
    public async Task AppUser_CannotInsertForAnotherTenant()
    {
        await using var connection = await OpenAsync(fixture.AppUserConnectionString, Guid.NewGuid());
        await using var command = new NpgsqlCommand(
            "INSERT INTO lancamentos (id, tenant_id, tipo, descricao, valor, vencimento, status, created_at, is_deleted) " +
            "VALUES (gen_random_uuid(), gen_random_uuid(), 'Honorario', 'x', 10, current_date, 'Pendente', now(), false)", connection);

        var act = () => command.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Theory]
    [InlineData("Pendente", true, 10, "pago_em")]
    [InlineData("Pago", false, 10, "pago_em")]
    [InlineData("Pendente", false, 0, "valor")]
    public async Task Database_RejectsInconsistentEntries(string status, bool comPagoEm, decimal valor, string constraintHint)
    {
        var act = () => SeedAsync(Guid.NewGuid(), status, comPagoEm, valor);

        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        exception.Which.ConstraintName.Should().Contain(constraintHint);
    }
}
