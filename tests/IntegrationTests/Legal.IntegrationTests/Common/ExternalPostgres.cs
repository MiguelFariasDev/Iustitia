using Npgsql;

namespace Advocacia.Modules.Legal.IntegrationTests.Common;

/// <summary>
/// Alternativa opt-in ao Testcontainers para ambientes SEM Docker (ex.: sessão de CI efêmera ou
/// sandbox de desenvolvimento): se <c>ADVOCACIA_TEST_PG</c> estiver definida com a connection
/// string de um PostgreSQL 16 SUPERUSER (ex.: <c>Host=localhost;Username=postgres;Password=postgres</c>),
/// cada fixture cria um banco descartável nele em vez de subir um container. Sem a variável, o
/// comportamento é o de sempre (Testcontainers).
/// </summary>
internal static class ExternalPostgres
{
    private const string EnvironmentVariable = "ADVOCACIA_TEST_PG";

    public static async Task<(string ConnectionString, string DatabaseName)?> TryCreateDatabaseAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            return null;
        }

        var databaseName = $"advocacia_test_{Guid.NewGuid():N}";

        await using var connection = new NpgsqlConnection(baseConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(baseConnection) { Database = databaseName };
        return (builder.ConnectionString, databaseName);
    }

    public static async Task DropDatabaseAsync(string databaseName)
    {
        var baseConnection = Environment.GetEnvironmentVariable(EnvironmentVariable)!;

        await using var connection = new NpgsqlConnection(baseConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
