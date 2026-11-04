using Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Legal.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Advocacia.Modules.Legal.IntegrationTests.Persistence;

/// <summary>
/// Isolamento multi-tenant da tabela publications (ver ADR-004 e
/// infra/supabase/migrations/011_publications_rls.sql).
///
/// Conecta como app_user, NÃO como o dono das tabelas: no Postgres, o dono e superusers
/// bypassam RLS por padrão, então um teste conectado como postgres validaria apenas o filtro
/// global do EF Core e passaria mesmo com as policies ausentes ou erradas.
///
/// Publicações são o dado mais sensível do sistema (inteiro teor de comunicações processuais,
/// sob sigilo profissional) — este é o teste que impede um vazamento entre escritórios.
/// </summary>
[Collection(LegalPostgresCollection.Name)]
public class PublicationTenantIsolationTests(LegalPostgresFixture fixture)
{
    [Fact]
    public async Task RlsIsEnabledOnPublications()
    {
        await using var connection = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT relrowsecurity FROM pg_class WHERE relname = 'publications'";

        var rowSecurityEnabled = (bool?)await command.ExecuteScalarAsync();

        rowSecurityEnabled.Should().BeTrue("011_publications_rls.sql deveria habilitar RLS em publications");
    }

    [Fact]
    public async Task AsAppUser_ATenantNeverSeesAnotherTenantsPublications()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await SeedAsync(tenantA, "ext-do-tenant-a");
        await SeedAsync(tenantB, "ext-do-tenant-b");

        await using var contextA = CreateAppUserContext(tenantA);
        var visibleToA = await new PublicationRepository(contextA).ListByTenantAsync(tenantA, null, null, 0, 100);

        visibleToA.Select(publication => publication.ExternalId).Should().Equal("ext-do-tenant-a");
    }

    [Fact]
    public async Task AsAppUser_IgnoreQueryFiltersStillCannotReachAnotherTenant()
    {
        // O teste que importa de verdade: IgnoreQueryFilters desliga a proteção do EF Core.
        // Se o RLS não estivesse no lugar, este seria o caminho por onde um bug de código
        // vazaria o inteiro teor de publicações de outro escritório.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await SeedAsync(tenantA, "ext-a-sem-filtro");
        await SeedAsync(tenantB, "ext-b-sem-filtro");

        await using var contextA = CreateAppUserContext(tenantA);

        var allRowsVisibleToA = await contextA.Publications.IgnoreQueryFilters().ToListAsync();

        allRowsVisibleToA.Should().OnlyContain(publication => publication.TenantId == tenantA);
        allRowsVisibleToA.Select(publication => publication.ExternalId).Should().Equal("ext-a-sem-filtro");
    }

    [Fact]
    public async Task AsAppUser_WithoutTenantInSession_SeesNothing()
    {
        // Fail closed: sem app.current_tenant definido, current_setting devolve NULL e a
        // comparação da policy nunca é verdadeira. Esquecer de configurar o tenant nega
        // acesso, em vez de liberar tudo.
        var tenantId = Guid.NewGuid();
        await SeedAsync(tenantId, "ext-fail-closed");

        await using var context = TestLegalDbContextFactory.Create(fixture.AppUserConnectionString);

        var rows = await context.Publications.IgnoreQueryFilters().ToListAsync();

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task AsAppUser_CannotInsertAPublicationForAnotherTenant()
    {
        // A policy de INSERT (WITH CHECK) é o que impede o job de captura de gravar em nome
        // do tenant errado — o caminho de código onde um tenant_id trocado seria mais fácil
        // de introduzir e mais difícil de notar.
        var sessionTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();

        await using var context = CreateAppUserContext(sessionTenant);
        new PublicationRepository(context).Add(TestPublications.Create(otherTenant, "ext-tenant-errado"));

        var act = () => context.SaveChangesAsync();

        var exception = await act.Should().ThrowAsync<DbUpdateException>();
        exception.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task AsAppUser_CanInsertAPublicationForItsOwnTenant()
    {
        var tenantId = Guid.NewGuid();

        await using var context = CreateAppUserContext(tenantId);
        new PublicationRepository(context).Add(TestPublications.Create(tenantId, "ext-tenant-correto"));

        var act = () => context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    private async Task SeedAsync(Guid tenantId, string externalId)
    {
        // Semeia como dono das tabelas (bypassa RLS) para preparar o cenário sem depender da
        // própria regra sob teste.
        await using var context = TestLegalDbContextFactory.Create(
            fixture.SuperuserConnectionString, TestLegalDbContextFactory.TenantContextFor(tenantId));

        new PublicationRepository(context).Add(TestPublications.Create(tenantId, externalId));
        await context.SaveChangesAsync();
    }

    private Infrastructure.Persistence.LegalDbContext CreateAppUserContext(Guid tenantId) =>
        TestLegalDbContextFactory.Create(
            fixture.AppUserConnectionString, TestLegalDbContextFactory.TenantContextFor(tenantId));
}
