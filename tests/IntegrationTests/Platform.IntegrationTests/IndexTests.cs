using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Npgsql;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>Confirma que os índices declarados via Fluent API e via SQL raw existem de verdade no banco.</summary>
[Collection(PostgresCollection.Name)]
public class IndexTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData("tenants", "ux_tenants_cnpj")]
    [InlineData("tenants", "ix_tenants_status")]
    [InlineData("users", "ux_users_tenant_email")]
    [InlineData("users", "ix_users_tenant_role")]
    [InlineData("users", "ix_users_tenant_active")]
    [InlineData("users", "ix_users_tenant_specialty")]
    [InlineData("users", "ix_users_tenant_active_only")]
    [InlineData("users", "ix_users_search")]
    [InlineData("users", "ix_users_name_trgm")]
    [InlineData("audit_logs", "ix_audit_logs_tenant_created")]
    [InlineData("audit_logs", "ix_audit_logs_entity")]
    [InlineData("audit_logs", "ix_audit_logs_user_created")]
    [InlineData("audit_logs", "ix_audit_logs_created_brin")]
    [InlineData("settings", "ux_settings_tenant_key")]
    [InlineData("feature_flags", "ux_feature_flags_tenant_key")]
    [InlineData("outbox_messages", "ix_outbox_pending")]
    [InlineData("outbox_messages", "ix_outbox_status_created")]
    public async Task Index_Exists(string table, string indexName)
    {
        var count = await CountIndexAsync(table, indexName);

        count.Should().Be(1, $"o índice {indexName} deveria existir na tabela {table}");
    }

    [Fact]
    public async Task Index_UsersSearch_UsesGinAccessMethod()
    {
        var accessMethod = await GetIndexAccessMethodAsync("ix_users_search");

        accessMethod.Should().Be("gin");
    }

    [Fact]
    public async Task Index_UsersNameTrgm_UsesGinAccessMethod()
    {
        var accessMethod = await GetIndexAccessMethodAsync("ix_users_name_trgm");

        accessMethod.Should().Be("gin");
    }

    [Fact]
    public async Task Index_AuditLogsCreatedBrin_UsesBrinAccessMethod()
    {
        var accessMethod = await GetIndexAccessMethodAsync("ix_audit_logs_created_brin");

        accessMethod.Should().Be("brin");
    }

    [Fact]
    public async Task Column_UsersSearchVector_ExistsAsGeneratedColumn()
    {
        await using var connection = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT is_generated FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'users' AND column_name = 'search_vector'
            """;

        var result = await command.ExecuteScalarAsync();

        result.Should().Be("ALWAYS", "users.search_vector deveria ser uma coluna gerada (GENERATED ALWAYS AS ... STORED)");
    }

    private async Task<long> CountIndexAsync(string table, string indexName)
    {
        await using var connection = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' AND tablename = @table AND indexname = @index";
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("index", indexName);

        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<string> GetIndexAccessMethodAsync(string indexName)
    {
        await using var connection = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT am.amname
            FROM pg_class i
            JOIN pg_am am ON am.oid = i.relam
            WHERE i.relname = @index
            """;
        command.Parameters.AddWithValue("index", indexName);

        var result = await command.ExecuteScalarAsync();
        return (string)result!;
    }
}
