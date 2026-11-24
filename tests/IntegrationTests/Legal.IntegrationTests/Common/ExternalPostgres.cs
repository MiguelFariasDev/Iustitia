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

    /// <summary>Chave do advisory lock que serializa a montagem de bancos de teste no cluster.</summary>
    private const long ClusterLockKey = 7_301_024_531;

    /// <summary>
    /// Serializa a MONTAGEM do banco de teste no cluster (e só ela). As roles do PostgreSQL (app_user, as do
    /// Supabase) são do cluster inteiro, não do banco: dois fixtures montando ao mesmo tempo — coleções do xUnit
    /// rodam em paralelo — executam <c>ALTER ROLE</c>/<c>GRANT</c> sobre a MESMA role e o PostgreSQL responde
    /// <c>XX000: tuple concurrently updated</c>, derrubando todos os testes da coleção de uma vez. Com
    /// Testcontainers cada fixture tem o próprio servidor e isso não ocorre; com o servidor único do CI, sim.
    ///
    /// É um advisory lock do banco, não um semáforo em memória, para valer também entre PROCESSOS de teste
    /// diferentes que usem o mesmo servidor. Sem <c>ADVOCACIA_TEST_PG</c> (Testcontainers) não faz nada.
    /// </summary>
    public static async Task<IAsyncDisposable> AcquireClusterLockAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            return NoLock.Instance;
        }

        var connection = new NpgsqlConnection(baseConnection);
        await connection.OpenAsync();
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", connection))
        {
            command.Parameters.AddWithValue("key", ClusterLockKey);
            await command.ExecuteNonQueryAsync();
        }

        return new HeldLock(connection);
    }

    private sealed class NoLock : IAsyncDisposable
    {
        public static readonly NoLock Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Fechar a conexão libera o lock de sessão; o unlock explícito só deixa a intenção clara.</summary>
    private sealed class HeldLock(NpgsqlConnection connection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", connection);
                command.Parameters.AddWithValue("key", ClusterLockKey);
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
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
