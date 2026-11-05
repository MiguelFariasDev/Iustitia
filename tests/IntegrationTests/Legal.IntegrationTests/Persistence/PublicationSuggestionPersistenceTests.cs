using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Suggestions;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Legal.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Advocacia.Modules.Legal.IntegrationTests.Persistence;

/// <summary>
/// Persistência das sugestões de IA e da auditoria de uso (Etapa 3.2 — ver ADR-070 e
/// infra/supabase/migrations/014_publication_suggestions.sql). Os testes de RLS conectam como
/// app_user: como dono das tabelas o RLS é bypassado e o teste passaria com as policies ausentes.
/// </summary>
[Collection(LegalPostgresCollection.Name)]
public class PublicationSuggestionPersistenceTests(LegalPostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = TestPublications.Now;

    [Fact]
    public async Task RlsIsEnabled()
    {
        await using var connection = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT relrowsecurity FROM pg_class WHERE relname = 'publication_suggestions'";

        ((bool?)await command.ExecuteScalarAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Suggestion_RoundTrips_AndStoresDeadlineTypeInLowercase()
    {
        var tenantId = Guid.NewGuid();
        var publication = await SeedPublicationAsync(tenantId, "ext-roundtrip");
        var suggestion = NewSuggestion(tenantId, publication, days: 15, DeadlineType.Business, confidence: 88);

        await using (var context = SuperuserContext(tenantId))
        {
            new PublicationSuggestionRepository(context).Add(suggestion);
            await context.SaveChangesAsync();
        }

        await using var readContext = SuperuserContext(tenantId);
        var loaded = await new PublicationSuggestionRepository(readContext)
            .GetByPublicationIdAsync(tenantId, publication.Id);

        loaded.Should().NotBeNull();
        loaded!.ActType.Should().Be("intimacao");
        loaded.SuggestedDeadlineDays.Should().Be(15);
        loaded.DeadlineType.Should().Be(DeadlineType.Business);
        loaded.Confidence.Should().Be(88);
        loaded.RawResponse.Should().Contain("classification");

        (await ScalarAsync("SELECT deadline_type FROM publication_suggestions WHERE publication_id = @id", publication.Id.Value))
            .Should().Be("business");
    }

    [Fact]
    public async Task Suggestion_WithoutDeadline_StoresNulls()
    {
        var tenantId = Guid.NewGuid();
        var publication = await SeedPublicationAsync(tenantId, "ext-sem-prazo");

        await using (var context = SuperuserContext(tenantId))
        {
            new PublicationSuggestionRepository(context)
                .Add(NewSuggestion(tenantId, publication, days: null, type: null, confidence: 70));
            await context.SaveChangesAsync();
        }

        await using var readContext = SuperuserContext(tenantId);
        var loaded = await new PublicationSuggestionRepository(readContext).GetByPublicationIdAsync(tenantId, publication.Id);

        loaded!.SuggestedDeadlineDays.Should().BeNull();
        loaded.DeadlineType.Should().BeNull();
    }

    [Fact]
    public async Task Suggestion_SecondOneForTheSamePublication_ViolatesTheUniqueIndex()
    {
        var tenantId = Guid.NewGuid();
        var publication = await SeedPublicationAsync(tenantId, "ext-unica");

        await using (var first = SuperuserContext(tenantId))
        {
            new PublicationSuggestionRepository(first).Add(NewSuggestion(tenantId, publication, 15, DeadlineType.Business, 90));
            await first.SaveChangesAsync();
        }

        await using var second = SuperuserContext(tenantId);
        new PublicationSuggestionRepository(second).Add(NewSuggestion(tenantId, publication, 5, DeadlineType.Business, 90));

        var act = () => second.SaveChangesAsync();

        var exception = await act.Should().ThrowAsync<DbUpdateException>();
        exception.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task ListAwaitingSuggestion_ReturnsOnlyPendingPublicationsWithoutSuggestion_OldestFirst()
    {
        var tenantId = Guid.NewGuid();
        await SeedPublicationAsync(tenantId, "ext-antiga", publishedAt: Now.AddDays(-10));
        await SeedPublicationAsync(tenantId, "ext-recente", publishedAt: Now.AddDays(-1));
        var withSuggestion = await SeedPublicationAsync(tenantId, "ext-com-sugestao", publishedAt: Now.AddDays(-5));
        await SeedPublicationAsync(tenantId, "ext-revisada", publishedAt: Now.AddDays(-6));

        await using (var context = SuperuserContext(tenantId))
        {
            new PublicationSuggestionRepository(context)
                .Add(NewSuggestion(tenantId, withSuggestion, 15, DeadlineType.Business, 90));

            var tracked = await context.Publications.FirstAsync(publication => publication.ExternalId == "ext-revisada");
            tracked.ChangeStatus(PublicationStatus.Approved).IsSuccess.Should().BeTrue();

            await context.SaveChangesAsync();
        }

        await using var readContext = SuperuserContext(tenantId);
        var pending = await new PublicationSuggestionRepository(readContext).ListAwaitingSuggestionAsync(tenantId, 0, 10);

        pending.Select(publication => publication.ExternalId).Should().Equal("ext-antiga", "ext-recente");
    }

    [Fact]
    public async Task ListAwaitingSuggestion_PagesWithSkip()
    {
        var tenantId = Guid.NewGuid();
        await SeedPublicationAsync(tenantId, "ext-p1", publishedAt: Now.AddDays(-3));
        await SeedPublicationAsync(tenantId, "ext-p2", publishedAt: Now.AddDays(-2));
        await SeedPublicationAsync(tenantId, "ext-p3", publishedAt: Now.AddDays(-1));

        await using var context = SuperuserContext(tenantId);
        var repository = new PublicationSuggestionRepository(context);

        (await repository.ListAwaitingSuggestionAsync(tenantId, 0, 2)).Select(p => p.ExternalId).Should().Equal("ext-p1", "ext-p2");
        (await repository.ListAwaitingSuggestionAsync(tenantId, 2, 2)).Select(p => p.ExternalId).Should().Equal("ext-p3");
    }

    [Fact]
    public async Task ListTenantsAwaitingSuggestion_FindsTenantsWithPendingWork()
    {
        var withWork = Guid.NewGuid();
        var withoutWork = Guid.NewGuid();
        await SeedPublicationAsync(withWork, "ext-trabalho");
        var done = await SeedPublicationAsync(withoutWork, "ext-feita");

        await using (var context = SuperuserContext(withoutWork))
        {
            new PublicationSuggestionRepository(context).Add(NewSuggestion(withoutWork, done, 15, DeadlineType.Business, 90));
            await context.SaveChangesAsync();
        }

        await using var readContext = TestLegalDbContextFactory.Create(fixture.SuperuserConnectionString);
        var tenants = await new PublicationSuggestionRepository(readContext).ListTenantsAwaitingSuggestionAsync();

        tenants.Should().Contain(withWork);
        tenants.Should().NotContain(withoutWork);
    }

    [Fact]
    public async Task AsAppUser_ATenantNeverSeesAnotherTenantsSuggestions()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var publicationA = await SeedPublicationAsync(tenantA, "ext-rls-a");
        var publicationB = await SeedPublicationAsync(tenantB, "ext-rls-b");

        await SeedSuggestionAsync(tenantA, publicationA);
        await SeedSuggestionAsync(tenantB, publicationB);

        await using var contextA = AppUserContext(tenantA);
        var visible = await contextA.PublicationSuggestions.IgnoreQueryFilters().ToListAsync();

        visible.Should().OnlyContain(suggestion => suggestion.TenantId == tenantA);
        visible.Select(suggestion => suggestion.PublicationId).Should().Equal(publicationA.Id);
    }

    [Fact]
    public async Task AsAppUser_CannotInsertASuggestionForAnotherTenant()
    {
        var sessionTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var publication = await SeedPublicationAsync(otherTenant, "ext-rls-insert");

        await using var context = AppUserContext(sessionTenant);
        new PublicationSuggestionRepository(context)
            .Add(NewSuggestion(otherTenant, publication, 15, DeadlineType.Business, 90));

        var act = () => context.SaveChangesAsync();

        var exception = await act.Should().ThrowAsync<DbUpdateException>();
        exception.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task Database_RejectsAConfidenceOutsideTheContractEvenIfTheApplicationAllowedIt()
    {
        var tenantId = Guid.NewGuid();
        var publication = await SeedPublicationAsync(tenantId, "ext-check");

        await using var connection = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await connection.OpenAsync();

        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO publication_suggestions
              (id, tenant_id, publication_id, act_type, confidence, requires_manual_review, content_truncated,
               ai_provider, ai_model, prompt_version, raw_response, created_at)
            VALUES
              (@id, @tenant, @publication, 'despacho', 150, false, false, 'Anthropic', 'm', '1.0.0', '{}'::jsonb, now())
            """;
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("tenant", tenantId);
        insert.Parameters.AddWithValue("publication", publication.Id.Value);

        var act = () => insert.ExecuteNonQueryAsync();

        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    private async Task<Publication> SeedPublicationAsync(Guid tenantId, string externalId, DateTimeOffset? publishedAt = null)
    {
        var publication = TestPublications.Create(tenantId, externalId, publishedAt: publishedAt);

        await using var context = SuperuserContext(tenantId);
        new PublicationRepository(context).Add(publication);
        await context.SaveChangesAsync();

        return publication;
    }

    private async Task SeedSuggestionAsync(Guid tenantId, Publication publication)
    {
        await using var context = SuperuserContext(tenantId);
        new PublicationSuggestionRepository(context).Add(NewSuggestion(tenantId, publication, 15, DeadlineType.Business, 90));
        await context.SaveChangesAsync();
    }

    private static PublicationSuggestion NewSuggestion(
        Guid tenantId, Publication publication, int? days, DeadlineType? type, int confidence) =>
        PublicationSuggestion.Create(
            tenantId,
            publication.Id,
            "intimacao",
            days,
            type,
            "CPC, art. 1.003, §5º",
            confidence,
            contentTruncated: false,
            "Anthropic",
            "claude-sonnet-5-5",
            "1.0.0/1.0.0",
            "{\"classification\":{\"act_type\":\"intimacao\"},\"deadline\":null}",
            Now).Value;

    private LegalDbContext SuperuserContext(Guid tenantId) =>
        TestLegalDbContextFactory.Create(fixture.SuperuserConnectionString, TestLegalDbContextFactory.TenantContextFor(tenantId));

    private LegalDbContext AppUserContext(Guid tenantId) =>
        TestLegalDbContextFactory.Create(fixture.AppUserConnectionString, TestLegalDbContextFactory.TenantContextFor(tenantId));

    private async Task<object?> ScalarAsync(string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("id", id);

        return await command.ExecuteScalarAsync();
    }
}
