using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Persistence;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Advocacia.Modules.Legal.IntegrationTests.Common;

/// <summary>
/// Sobe um PostgreSQL 16 real via Testcontainers e monta o banco completo, na ordem em que um
/// ambiente real é montado:
///   1. migrations do EF Core do Platform (tabelas do núcleo, incluindo outbox_messages);
///   2. migrations do EF Core do Legal (tabela publications);
///   3. migrations do EF Core do Anthropic (tabela ai_usage_logs);
///   4. migrations SQL do Supabase (/infra/supabase/migrations), que dependem das tabelas já
///      existirem — 010 adiciona índices GIN/BRIN em publications e 011 habilita o RLS dela.
///
/// Expõe duas connection strings: uma como dono das tabelas (postgres, que BYPASSA RLS por
/// padrão no Postgres) e outra como app_user, que é a única com que se pode testar RLS de
/// verdade.
/// </summary>
public sealed class LegalPostgresFixture : IAsyncLifetime
{
    private const string AppUserPassword = "legal_integration_tests_app_user";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("advocacia_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string SuperuserConnectionString => _container.GetConnectionString();

    public string AppUserConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await MigrateAsync<PlatformDbContext>(
            options => new PlatformDbContext(options, new AsyncLocalTenantContext()));

        await MigrateAsync<LegalDbContext>(
            options => new LegalDbContext(options, new AsyncLocalTenantContext()));

        await MigrateAsync<AnthropicDbContext>(
            options => new AnthropicDbContext(options, new AsyncLocalTenantContext()));

        await ApplySupabaseSqlMigrationsAsync();

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(SuperuserConnectionString)
        {
            Username = "app_user",
            Password = AppUserPassword,
        };
        AppUserConnectionString = connectionStringBuilder.ConnectionString;
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Constrói o DbContext com um AsyncLocalTenantContext "vazio" — o mesmo tipo que os
    /// testes usarão depois. O EF Core cacheia o modelo compilado (com os HasQueryFilter já
    /// fechados) por TIPO de DbContext, não por instância, então a primeira instância criada
    /// no processo define o modelo para todas (ver comentário em BaseDbContext).
    /// </summary>
    private async Task MigrateAsync<TContext>(Func<DbContextOptions<TContext>, TContext> factory)
        where TContext : Microsoft.EntityFrameworkCore.DbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.UseNpgsql(SuperuserConnectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        await using var context = factory(optionsBuilder.Options);
        await context.Database.MigrateAsync();
    }

    private async Task ApplySupabaseSqlMigrationsAsync()
    {
        var migrationsDirectory = FindRepositoryPath("infra", "supabase", "migrations");
        var sqlFiles = Directory.GetFiles(migrationsDirectory, "*.sql").OrderBy(file => file, StringComparer.Ordinal);

        await using var connection = new NpgsqlConnection(SuperuserConnectionString);
        await connection.OpenAsync();

        await CreateSupabaseSystemRolesAsync(connection);

        foreach (var file in sqlFiles)
        {
            var sql = await File.ReadAllTextAsync(file);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        // Senha só existe durante a vida deste container — nunca é persistida.
        await using var passwordCommand = connection.CreateCommand();
        passwordCommand.CommandText = $"ALTER ROLE app_user WITH PASSWORD '{AppUserPassword}'";
        await passwordCommand.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Um projeto Supabase real já vem com essas roles; o Postgres puro do Testcontainers não,
    /// e 004_custom_access_token_hook.sql concede grants para elas (ver PostgresFixture em
    /// Platform.IntegrationTests, mesma razão).
    /// </summary>
    private static async Task CreateSupabaseSystemRolesAsync(NpgsqlConnection connection)
    {
        foreach (var role in new[] { "supabase_auth_admin", "authenticated", "anon", "service_role" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                DO $$
                BEGIN
                  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '{role}') THEN
                    CREATE ROLE {role};
                  END IF;
                END
                $$;
                """;
            await command.ExecuteNonQueryAsync();
        }
    }

    internal static string FindRepositoryPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. segments]);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Não foi possível localizar {Path.Combine(segments)} a partir de {AppContext.BaseDirectory}.");
    }
}
