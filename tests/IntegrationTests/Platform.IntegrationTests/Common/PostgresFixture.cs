using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Modules.Atendimentos.Infrastructure.Persistence;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Persistence;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Advocacia.Platform.IntegrationTests.Common;

/// <summary>
/// Sobe um PostgreSQL 16 real via Testcontainers, aplica as migrations do EF Core de TODOS
/// os módulos (Platform, Legal e Atendimento) e, em seguida, as migrations SQL do Supabase (extensões,
/// RLS, role app_user — ver /infra/supabase/migrations), uma única vez por classe de teste
/// que a usa.
///
/// A ordem importa: as migrations SQL a partir da 010 criam índices e policies sobre
/// publications (tabela do módulo Legal), então o schema de Legal precisa existir antes —
/// é por isso que este fixture, embora seja dos testes de Platform, também migra o
/// LegalDbContext.
///
/// Sem Docker (ex.: CI sem daemon, sandbox), aponte <c>ADVOCACIA_TEST_PG_SERVER</c> para um
/// servidor PostgreSQL 16 já em pé, com um superusuário — ex.:
/// <c>Host=127.0.0.1;Port=5544;Username=postgres;Password=postgres</c>. A fixture cria um banco
/// descartável nesse servidor e o remove no fim; o servidor nunca é tocado além disso.
///
/// Expõe duas connection strings: uma como superuser/dono das tabelas (postgres —
/// bypassa RLS) e outra como app_user (respeita RLS de verdade).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string AppUserPassword = "integration_tests_app_user";

    private const string ExternalServerVariable = "ADVOCACIA_TEST_PG_SERVER";

    private readonly string? _externalServer = Environment.GetEnvironmentVariable(ExternalServerVariable);
    private readonly string _externalDatabase = $"advocacia_it_{Guid.NewGuid():N}";

    private readonly PostgreSqlContainer? _container;

    public PostgresFixture()
    {
        if (string.IsNullOrWhiteSpace(_externalServer))
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("advocacia_db")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
        }
    }

    public string SuperuserConnectionString => _container is not null
        ? _container.GetConnectionString()
        : new NpgsqlConnectionStringBuilder(_externalServer!) { Database = _externalDatabase }.ConnectionString;
    // Criado sob demanda: PostgreSqlBuilder.Build() exige Docker já no Build(), o que impediria
    // usar o banco externo (ADVOCACIA_TEST_PG) em ambientes sem Docker.
    private PostgreSqlContainer? _container;

    private string? _externalDatabaseName;

    public string SuperuserConnectionString { get; private set; } = null!;

    public string AppUserConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
        }
        else
        {
            await CreateExternalDatabaseAsync();
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

        var optionsBuilder = new DbContextOptionsBuilder<PlatformDbContext>();
        optionsBuilder.UseNpgsql(SuperuserConnectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        // Importante: NUNCA construir PlatformDbContext com um tenantContext "provisório"
        // diferente do que os testes usarão depois — o EF Core cacheia o modelo compilado
        // (com os HasQueryFilter já fechados) por TIPO de DbContext, não por instância, e
        // esta é a PRIMEIRA instância criada no processo (ver comentário em BaseDbContext).
        await using (var context = new PlatformDbContext(optionsBuilder.Options, new AsyncLocalTenantContext()))
        {
            await context.Database.MigrateAsync();
        }

        var legalOptionsBuilder = new DbContextOptionsBuilder<LegalDbContext>();
        legalOptionsBuilder.UseNpgsql(SuperuserConnectionString);
        legalOptionsBuilder.UseSnakeCaseNamingConvention();

        await using (var legalContext = new LegalDbContext(legalOptionsBuilder.Options, new AsyncLocalTenantContext()))
        {
            await legalContext.Database.MigrateAsync();
        }

        var atendimentoOptionsBuilder = new DbContextOptionsBuilder<AtendimentoDbContext>();
        atendimentoOptionsBuilder.UseNpgsql(SuperuserConnectionString);
        atendimentoOptionsBuilder.UseSnakeCaseNamingConvention();

        await using (var atendimentoContext = new AtendimentoDbContext(atendimentoOptionsBuilder.Options, new AsyncLocalTenantContext()))
        {
            await atendimentoContext.Database.MigrateAsync();
        // ai_usage_logs (módulo Anthropic): 015_ai_usage_logs.sql precisa da tabela existente.
        var anthropicOptionsBuilder = new DbContextOptionsBuilder<AnthropicDbContext>();
        anthropicOptionsBuilder.UseNpgsql(SuperuserConnectionString);
        anthropicOptionsBuilder.UseSnakeCaseNamingConvention();

        await using (var anthropicContext = new AnthropicDbContext(anthropicOptionsBuilder.Options, new AsyncLocalTenantContext()))
        {
            await anthropicContext.Database.MigrateAsync();
        }

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
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        await DropExternalDatabaseAsync();
    }

    private async Task CreateExternalDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(_externalServer!) { Database = "postgres" }.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{_externalDatabase}\"";
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropExternalDatabaseAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(_externalServer!) { Database = "postgres" }.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{_externalDatabase}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
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

    private async Task ApplySupabaseSqlMigrationsAsync()
    {
        var migrationsDirectory = FindSupabaseMigrationsDirectory();
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

        // Senha só existe durante a vida deste container Testcontainers — nunca é persistida.
        await using var passwordCommand = connection.CreateCommand();
        passwordCommand.CommandText = $"ALTER ROLE app_user WITH PASSWORD '{AppUserPassword}'";
        await passwordCommand.ExecuteNonQueryAsync();

        // O Hangfire (storage no PostgreSQL) cria o próprio schema na primeira inicialização, e o
        // host de teste conecta como app_user. Em produção esse schema é provisionado por quem
        // tem o privilégio; aqui o container é descartável, então basta conceder CREATE no banco.
        await using var hangfireGrant = connection.CreateCommand();
        hangfireGrant.CommandText = $"GRANT CREATE ON DATABASE \"{new NpgsqlConnectionStringBuilder(SuperuserConnectionString).Database}\" TO app_user";
        hangfireGrant.CommandText = "DO $$ BEGIN EXECUTE format('GRANT CREATE ON DATABASE %I TO app_user', current_database()); END $$";
        await hangfireGrant.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Um projeto Supabase real já vem com essas roles pré-criadas (usadas internamente pelo
    /// GoTrue/PostgREST); o Postgres puro do Testcontainers não as tem, e
    /// 004_custom_access_token_hook.sql concede grants para elas — sem criá-las aqui antes,
    /// a migration falharia com "role does not exist" neste ambiente de teste.
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

    private static string FindSupabaseMigrationsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "infra", "supabase", "migrations");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Não foi possível localizar infra/supabase/migrations a partir do diretório de teste " +
            $"({AppContext.BaseDirectory}). Os testes de integração dependem das migrations SQL do Supabase.");
    }
}
