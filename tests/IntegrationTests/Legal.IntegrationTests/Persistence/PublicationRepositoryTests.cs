using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.IntegrationTests.Common;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Advocacia.Modules.Legal.IntegrationTests.Persistence;

[Collection(LegalPostgresCollection.Name)]
public class PublicationRepositoryTests(LegalPostgresFixture fixture)
{
    [Fact]
    public async Task Add_ThenGetById_RoundTripsEveryFieldAndValueObject()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        var publication = TestPublications.Create(
            tenantId, "ext-roundtrip", TestPublications.CnjNumberA, content: "<p>Intimação   para manifestação.</p>");
        repository.Add(publication);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var loaded = await repository.GetByIdAsync(publication.Id);

        loaded.Should().NotBeNull();
        loaded!.TenantId.Should().Be(tenantId);
        loaded.ExternalId.Should().Be("ext-roundtrip");
        loaded.Source.Should().Be(PublicationSource.DJEN);
        loaded.Status.Should().Be(PublicationStatus.PendingReview);
        loaded.IsRead.Should().BeFalse();
        loaded.ProcessId.Should().BeNull();

        // Os value objects sobrevivem à ida e volta pelas conversões do EF Core.
        loaded.CnjNumber.Value.Should().Be(TestPublications.CnjNumberA);
        loaded.CnjNumber.Digits.Should().Be("12345674820268260001");
        loaded.RawContent.Value.Should().Be("Intimação para manifestação.");
        loaded.PublishedAt.Value.Should().Be(TestPublications.Now.AddDays(-1));
    }

    [Fact]
    public async Task Add_WritesThePublicationCapturedEventToTheOutbox_InTheSameTransaction()
    {
        // O LegalDbContext mapeia outbox_messages (excluída das suas migrations) exatamente
        // para isto: sem o mapeamento, o OutboxInterceptor não teria onde gravar e nenhum
        // evento de publicação chegaria à mensageria (ver ADR-008).
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        var publication = TestPublications.Create(tenantId, "ext-outbox");
        repository.Add(publication);
        await context.SaveChangesAsync();

        var outboxMessages = await context.Set<OutboxMessage>()
            .Where(message => message.AggregateId == publication.Id.Value)
            .ToListAsync();

        outboxMessages.Should().ContainSingle()
            .Which.EventType.Should().Contain("PublicationCapturedEvent");
    }

    [Fact]
    public async Task Add_StampsTheAuditColumns()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        var publication = TestPublications.Create(tenantId, "ext-audit");
        repository.Add(publication);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var loaded = await repository.GetByIdAsync(publication.Id);

        loaded!.CreatedAt.Should().NotBe(default);
        loaded.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Update_PersistsTheStatusTransition()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        var publication = TestPublications.Create(tenantId, "ext-update");
        repository.Add(publication);
        await context.SaveChangesAsync();

        publication.ChangeStatus(PublicationStatus.Approved, "revisado").IsSuccess.Should().BeTrue();
        publication.MarkAsRead();
        repository.Update(publication);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var loaded = await repository.GetByIdAsync(publication.Id);

        loaded!.Status.Should().Be(PublicationStatus.Approved);
        loaded.ReviewNotes.Should().Be("revisado");
        loaded.IsRead.Should().BeTrue();
        loaded.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByExternalIdAsync_FindsThePublication()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        repository.Add(TestPublications.Create(tenantId, "ext-lookup"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var found = await repository.GetByExternalIdAsync(tenantId, "ext-lookup", PublicationSource.DJEN);
        var notFound = await repository.GetByExternalIdAsync(tenantId, "ext-inexistente", PublicationSource.DJEN);

        found.Should().NotBeNull();
        notFound.Should().BeNull();
    }

    [Fact]
    public async Task ExistsByExternalIdAsync_ReflectsPersistence()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        repository.Add(TestPublications.Create(tenantId, "ext-exists"));
        await context.SaveChangesAsync();

        (await repository.ExistsByExternalIdAsync(tenantId, "ext-exists", PublicationSource.DJEN)).Should().BeTrue();
        (await repository.ExistsByExternalIdAsync(tenantId, "ext-outro", PublicationSource.DJEN)).Should().BeFalse();
    }

    [Fact]
    public async Task FindExistingExternalIdsAsync_ReturnsOnlyThePersistedOnes()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        repository.AddRange([
            TestPublications.Create(tenantId, "ext-1"),
            TestPublications.Create(tenantId, "ext-2", TestPublications.CnjNumberB),
        ]);
        await context.SaveChangesAsync();

        var existing = await repository.FindExistingExternalIdsAsync(
            tenantId, ["ext-1", "ext-2", "ext-3"], PublicationSource.DJEN);

        existing.Should().BeEquivalentTo(["ext-1", "ext-2"]);
    }

    [Fact]
    public async Task FindExistingExternalIdsAsync_WithNoIds_DoesNotQueryTheDatabase()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        var existing = await repository.FindExistingExternalIdsAsync(tenantId, [], PublicationSource.DJEN);

        existing.Should().BeEmpty();
    }

    [Fact]
    public async Task UniqueIndex_RejectsADuplicatedExternalIdWithinTheSameTenant()
    {
        // A checagem em código é a primeira camada de deduplicação; este índice é a que
        // realmente segura duas execuções concorrentes do job (ver ADR-056).
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        repository.Add(TestPublications.Create(tenantId, "ext-dup"));
        await context.SaveChangesAsync();

        repository.Add(TestPublications.Create(tenantId, "ext-dup", TestPublications.CnjNumberB));

        var act = () => context.SaveChangesAsync();

        var exception = await act.Should().ThrowAsync<DbUpdateException>();
        exception.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task UniqueIndex_AllowsTheSameExternalIdInADifferentTenant()
    {
        // O mesmo id de comunicação do CNJ pode legitimamente interessar a dois escritórios
        // (partes/advogados distintos no mesmo processo): a unicidade é POR tenant.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var contextA = CreateContext(tenantA))
        {
            new PublicationRepository(contextA).Add(TestPublications.Create(tenantA, "ext-compartilhado"));
            await contextA.SaveChangesAsync();
        }

        await using var contextB = CreateContext(tenantB);
        new PublicationRepository(contextB).Add(TestPublications.Create(tenantB, "ext-compartilhado"));

        var act = () => contextB.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ListByProcessAsync_ReturnsTheProcessTimelineNewestFirst()
    {
        var tenantId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        repository.AddRange([
            TestPublications.Create(tenantId, "ext-old", processId: processId, publishedAt: TestPublications.Now.AddDays(-10)),
            TestPublications.Create(tenantId, "ext-new", TestPublications.CnjNumberB, processId, TestPublications.Now.AddDays(-1)),
            TestPublications.Create(tenantId, "ext-other-process", TestPublications.CnjNumberC, Guid.NewGuid()),
        ]);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var timeline = await repository.ListByProcessAsync(tenantId, processId, skip: 0, take: 10);

        timeline.Select(publication => publication.ExternalId).Should().Equal("ext-new", "ext-old");
    }

    [Fact]
    public async Task ListByTenantAsync_FiltersByStatusAndReadState()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        var approved = TestPublications.Create(tenantId, "ext-approved");
        approved.ChangeStatus(PublicationStatus.Approved);
        var read = TestPublications.Create(tenantId, "ext-read", TestPublications.CnjNumberB);
        read.MarkAsRead();

        repository.AddRange([approved, read, TestPublications.Create(tenantId, "ext-plain", TestPublications.CnjNumberC)]);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var approvedOnly = await repository.ListByTenantAsync(tenantId, PublicationStatus.Approved, null, 0, 10);
        var unreadOnly = await repository.ListByTenantAsync(tenantId, null, isRead: false, 0, 10);

        approvedOnly.Select(publication => publication.ExternalId).Should().Equal("ext-approved");
        unreadOnly.Select(publication => publication.ExternalId).Should().BeEquivalentTo(["ext-approved", "ext-plain"]);
    }

    [Fact]
    public async Task ListByTenantAsync_PaginatesNewestFirst()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        repository.AddRange([
            TestPublications.Create(tenantId, "ext-d3", publishedAt: TestPublications.Now.AddDays(-3)),
            TestPublications.Create(tenantId, "ext-d1", TestPublications.CnjNumberB, publishedAt: TestPublications.Now.AddDays(-1)),
            TestPublications.Create(tenantId, "ext-d2", TestPublications.CnjNumberC, publishedAt: TestPublications.Now.AddDays(-2)),
        ]);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var firstPage = await repository.ListByTenantAsync(tenantId, null, null, skip: 0, take: 2);
        var secondPage = await repository.ListByTenantAsync(tenantId, null, null, skip: 2, take: 2);

        firstPage.Select(publication => publication.ExternalId).Should().Equal("ext-d1", "ext-d2");
        secondPage.Select(publication => publication.ExternalId).Should().Equal("ext-d3");
    }

    [Fact]
    public async Task ListPendingReviewAsync_ReturnsOldestFirst_TheWorkQueueOrder()
    {
        // Ordem invertida em relação às outras listagens de propósito: o que espera revisão há
        // mais tempo é o que corre mais risco de perda de prazo.
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        var approved = TestPublications.Create(tenantId, "ext-approved", publishedAt: TestPublications.Now.AddDays(-20));
        approved.ChangeStatus(PublicationStatus.Approved);

        repository.AddRange([
            approved,
            TestPublications.Create(tenantId, "ext-recent", TestPublications.CnjNumberB, publishedAt: TestPublications.Now.AddDays(-1)),
            TestPublications.Create(tenantId, "ext-oldest", TestPublications.CnjNumberC, publishedAt: TestPublications.Now.AddDays(-9)),
        ]);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var queue = await repository.ListPendingReviewAsync(tenantId, skip: 0, take: 10);

        queue.Select(publication => publication.ExternalId).Should().Equal("ext-oldest", "ext-recent");
    }

    [Fact]
    public async Task CountByTenantAsync_IgnoresPagination()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        repository.AddRange([
            TestPublications.Create(tenantId, "ext-c1"),
            TestPublications.Create(tenantId, "ext-c2", TestPublications.CnjNumberB),
            TestPublications.Create(tenantId, "ext-c3", TestPublications.CnjNumberC),
        ]);
        await context.SaveChangesAsync();

        (await repository.CountByTenantAsync(tenantId, null, null)).Should().Be(3);
    }

    [Fact]
    public async Task SoftDelete_HidesThePublicationFromEveryQuery()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var repository = new PublicationRepository(context);

        var publication = TestPublications.Create(tenantId, "ext-soft-delete");
        repository.Add(publication);
        await context.SaveChangesAsync();

        publication.IsDeleted = true;
        publication.DeletedAt = TestPublications.Now;
        repository.Update(publication);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        (await repository.GetByIdAsync(publication.Id)).Should().BeNull();
        (await repository.ListByTenantAsync(tenantId, null, null, 0, 10)).Should().BeEmpty();
    }

    /// <summary>
    /// Contexto como dono das tabelas (postgres): o RLS não é exercido aqui, só o filtro global
    /// do BaseDbContext. O isolamento real por RLS é testado em
    /// <see cref="PublicationTenantIsolationTests"/>, com a role app_user.
    /// </summary>
    private LegalDbContext CreateContext(Guid tenantId) =>
        TestLegalDbContextFactory.Create(
            fixture.SuperuserConnectionString, TestLegalDbContextFactory.TenantContextFor(tenantId));
}
