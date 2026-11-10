using Advocacia.BuildingBlocks.Infrastructure.Security;
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

    // Criado sob demanda: PostgreSqlBuilder.Build() exige Docker já no Build(), o que impediria
    // usar o banco externo (ADVOCACIA_TEST_PG) em ambientes sem Docker.
    private PostgreSqlContainer? _container;

    private string? _externalDatabaseName;

    public string SuperuserConnectionString { get; private set; } = null!;

    public string AppUserConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Estes testes montam o DbContext sem host, então ninguém chamou AddAdvocaciaFieldEncryption:
        // sem isto os conversores recusam gravar (fail closed, ADR-068). Chaves só de teste.
        FieldEncryption.Configure(new AesGcmFieldEncryptor(Microsoft.Extensions.Options.Options.Create(new FieldEncryptionOptions
        {
            ActiveKeyId = "test",
            Keys = new() { ["test"] = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray()) },
            BlindIndexKey = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 50)).ToArray()),
        })));

        var external = await ExternalPostgres.TryCreateDatabaseAsync();
        if (external is { } externalDatabase)
        {
            SuperuserConnectionString = externalDatabase.ConnectionString;
            _externalDatabaseName = externalDatabase.DatabaseName;
        }
        else
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("advocacia_db")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
            await _container.StartAsync();
            SuperuserConnectionString = _container.GetConnectionString();
        }

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

    public async Task DisposeAsync()
    {
        if (_externalDatabaseName is not null)
        {
            await ExternalPostgres.DropDatabaseAsync(_externalDatabaseName);
            return;
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

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
