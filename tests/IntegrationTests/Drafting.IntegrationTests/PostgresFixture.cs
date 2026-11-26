using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Advocacia.Modules.Drafting.IntegrationTests;

/// <summary>
/// Um PostgreSQL 16 real (Testcontainers) para testar o que o provedor em memória NÃO consegue:
/// a reserva atômica da fila (UPDATE condicional) sob concorrência de verdade. Cada teste ganha um
/// banco novo, criado com <c>EnsureCreated</c> a partir do próprio modelo do EF — não depende da
/// migration (que é gerada à parte) nem do RLS (testado nas migrações SQL).
///
/// Sem Docker: aponte <c>ADVOCACIA_TEST_PG</c> para um PostgreSQL SUPERUSER já em execução.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string? _external = Environment.GetEnvironmentVariable("ADVOCACIA_TEST_PG");
    private PostgreSqlContainer? _container;
    private string _admin = string.Empty;

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_external))
        {
            _admin = _external;

            return;
        }

        _container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await _container.StartAsync();
        _admin = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Banco novo com a MIGRATION do EF e os scripts SQL 016, 017 e 019 (RLS, grants, índices), como em produção.
    /// Devolve a conexão do dono e a de <c>app_user</c> (que respeita o RLS).
    /// </summary>
    public async Task<(string Dono, string AppUser)> NewMigratedDatabaseAsync()
    {
        var name = $"drafting_it_{Guid.NewGuid():N}";
        await ExecutarAsync(_admin, $"CREATE DATABASE \"{name}\"");
        var dono = new NpgsqlConnectionStringBuilder(_admin) { Database = name, MaxPoolSize = 20 }.ConnectionString;

        var options = new DbContextOptionsBuilder<DraftingDbContext>().UseNpgsql(dono).UseSnakeCaseNamingConvention().Options;
        await using (var db = new DraftingDbContext(options, new AsyncLocalTenantContext()))
        {
            await db.Database.MigrateAsync();
        }

        await ExecutarAsync(dono, """
            DO $$ BEGIN
              IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'app_user') THEN CREATE ROLE app_user WITH LOGIN; END IF;
            END $$;
            ALTER ROLE app_user WITH PASSWORD 'it_app_user';
            GRANT USAGE ON SCHEMA public TO app_user;
            """);
        foreach (var script in new[] { "016_drafting.sql", "017_normas_juridicas.sql", "020_drafting_casos.sql" })
        {
            await ExecutarAsync(dono, await File.ReadAllTextAsync(Path.Combine(MigracoesSql(), script)));
        }

        return (dono, new NpgsqlConnectionStringBuilder(dono) { Username = "app_user", Password = "it_app_user" }.ConnectionString);
    }

    public static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var connection = new NpgsqlConnection(conexao);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string MigracoesSql()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidato = Path.Combine(dir.FullName, "infra", "supabase", "migrations");
            if (Directory.Exists(candidato))
            {
                return candidato;
            }
        }

        throw new DirectoryNotFoundException("infra/supabase/migrations");
    }

    /// <summary>Cria um banco vazio com o esquema do módulo e devolve uma fábrica de contextos (um por "worker").</summary>
    public async Task<Func<DraftingDbContext>> NewDatabaseAsync()
    {
        var name = $"drafting_it_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(_admin))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{name}\"";
            await create.ExecuteNonQueryAsync();
        }

        var connection = new NpgsqlConnectionStringBuilder(_admin) { Database = name, Pooling = true, MaxPoolSize = 50 }.ConnectionString;

        DraftingDbContext Create()
        {
            var options = new DbContextOptionsBuilder<DraftingDbContext>()
                .UseNpgsql(connection)
                .UseSnakeCaseNamingConvention()
                .Options;

            return new DraftingDbContext(options, new AsyncLocalTenantContext());
        }

        await using (var db = Create())
        {
            await db.Database.EnsureCreatedAsync();
        }

        return Create;
    }
}
