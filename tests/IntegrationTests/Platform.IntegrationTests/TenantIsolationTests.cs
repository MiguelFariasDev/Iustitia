using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Platform.Domain.Identity;
using Advocacia.Platform.Domain.Tenancy;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>
/// Valida o isolamento por tenant na camada mais forte possível: RLS no Postgres,
/// conectando como <c>app_user</c> (não o dono das tabelas — RLS não se aplica ao
/// dono/superuser por padrão). Ver ADR-004 e infra/supabase/migrations/002_rls.sql.
/// </summary>
[Collection(PostgresCollection.Name)]
public class TenantIsolationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Rls_WhenConnectedAsAppUserWithTenantSet_OnlySeesOwnTenantData()
    {
        Guid tenantAId;
        Guid tenantBId;

        await using (var setupContext = TestDbContextFactory.Create(fixture.SuperuserConnectionString))
        {
            var tenantA = Tenant.Create("RLS Tenant A", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;
            var tenantB = Tenant.Create("RLS Tenant B", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;
            setupContext.Tenants.AddRange(tenantA, tenantB);
            await setupContext.SaveChangesAsync();

            var userA = User.Create(UserId.New(), tenantA.Id.Value, "Usuário A", Email.Create($"{Guid.NewGuid()}@teste.com").Value, UserRole.Lawyer).Value;
            var userB = User.Create(UserId.New(), tenantB.Id.Value, "Usuário B", Email.Create($"{Guid.NewGuid()}@teste.com").Value, UserRole.Lawyer).Value;
            setupContext.Users.AddRange(userA, userB);
            await setupContext.SaveChangesAsync();

            tenantAId = tenantA.Id.Value;
            tenantBId = tenantB.Id.Value;
        }

        var tenantContext = new AsyncLocalTenantContext();
        tenantContext.SetTenant(tenantAId);

        await using var appUserContext = TestDbContextFactory.Create(fixture.AppUserConnectionString, tenantContext);

        var visibleUsers = await appUserContext.Users.ToListAsync();
        var visibleTenants = await appUserContext.Tenants.ToListAsync();

        visibleUsers.Should().ContainSingle(u => u.TenantId == tenantAId);
        visibleUsers.Should().NotContain(u => u.TenantId == tenantBId);
        visibleTenants.Should().ContainSingle(t => t.Id.Value == tenantAId);
        visibleTenants.Should().NotContain(t => t.Id.Value == tenantBId);
    }

    [Fact]
    public async Task Rls_WhenConnectedAsAppUserWithoutTenantSet_SeesNoRows()
    {
        await using (var setupContext = TestDbContextFactory.Create(fixture.SuperuserConnectionString))
        {
            var tenant = Tenant.Create("RLS sem tenant definido", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;
            setupContext.Tenants.Add(tenant);
            await setupContext.SaveChangesAsync();
        }

        // Nenhum SetTenant foi chamado: TenantContextInterceptor não emite SET nenhum, e a
        // policy de RLS nega qualquer linha por padrão (fail closed) quando o parâmetro de
        // sessão não está definido — IgnoreQueryFilters() isola esse comportamento do filtro
        // C# do EF Core (que nem existe para Tenant, já que ele não implementa IHasTenant).
        await using var appUserContext = TestDbContextFactory.Create(fixture.AppUserConnectionString);

        var visibleTenants = await appUserContext.Tenants.IgnoreQueryFilters().ToListAsync();

        visibleTenants.Should().BeEmpty("sem app.current_tenant definido, o RLS deve negar todo acesso (fail closed)");
    }

    [Fact]
    public async Task Rls_WhenConnectedAsSuperuser_BypassesRlsAndSeesAllTenants()
    {
        await using (var setupContext = TestDbContextFactory.Create(fixture.SuperuserConnectionString))
        {
            var tenant = Tenant.Create("RLS bypass superuser", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;
            setupContext.Tenants.Add(tenant);
            await setupContext.SaveChangesAsync();
        }

        await using var superuserContext = TestDbContextFactory.Create(fixture.SuperuserConnectionString);
        var count = await superuserContext.Tenants.IgnoreQueryFilters().CountAsync();

        count.Should().BeGreaterThanOrEqualTo(1, "o dono das tabelas (postgres) bypassa RLS por padrão no Postgres");
    }

    [Theory]
    [InlineData("tenants", "tenant_isolation_tenants")]
    [InlineData("users", "tenant_isolation_users")]
    // audit_logs é imutável: policies separadas de SELECT e INSERT (014_audit_immutability.sql).
    [InlineData("audit_logs", "audit_logs_select")]
    [InlineData("audit_logs", "audit_logs_insert")]
    [InlineData("settings", "tenant_isolation_settings")]
    [InlineData("feature_flags", "tenant_isolation_feature_flags")]
    public async Task RlsPolicy_Exists(string table, string policyName)
    {
        await using var connection = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM pg_policies WHERE schemaname = 'public' AND tablename = @table AND policyname = @policy";
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("policy", policyName);

        var count = (long)(await command.ExecuteScalarAsync())!;

        count.Should().Be(1, $"a policy {policyName} deveria existir na tabela {table}");
    }
}
