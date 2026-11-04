using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Platform.Domain.Identity;
using Advocacia.Platform.Domain.Tenancy;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Platform.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class PlatformDbContextTests(PostgresFixture fixture)
{
    [Fact]
    public async Task SaveChanges_WhenCreatingTenant_CanBeRetrievedById()
    {
        await using var context = TestDbContextFactory.Create(fixture.SuperuserConnectionString);

        var tenant = Tenant.Create("Escritório Integração", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        await using var readContext = TestDbContextFactory.Create(fixture.SuperuserConnectionString);
        var found = await readContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.Id);

        found.Should().NotBeNull();
        found!.Name.Should().Be("Escritório Integração");
    }

    [Fact]
    public async Task Remove_WhenEntityIsSoftDeletable_IsConvertedToUpdateAndHiddenByGlobalFilter()
    {
        await using var writeContext = TestDbContextFactory.Create(fixture.SuperuserConnectionString);
        var tenant = Tenant.Create("Tenant para excluir", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;
        writeContext.Tenants.Add(tenant);
        await writeContext.SaveChangesAsync();

        writeContext.Tenants.Remove(tenant);
        await writeContext.SaveChangesAsync();

        await using var readContext = TestDbContextFactory.Create(fixture.SuperuserConnectionString);
        var visible = await readContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.Id);
        visible.Should().BeNull("o filtro global de soft delete deve esconder registros com is_deleted = true");

        var ignoringFilters = await readContext.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenant.Id);
        ignoringFilters.Should().NotBeNull();
        ignoringFilters!.IsDeleted.Should().BeTrue();
        ignoringFilters.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveChanges_WhenCreatingEntity_FillsCreatedAtAndCreatedByViaAuditInterceptor()
    {
        var userId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser(userId);

        await using var context = TestDbContextFactory.Create(fixture.SuperuserConnectionString, currentUser: currentUser);
        var tenant = Tenant.Create("Tenant auditado", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;
        context.Tenants.Add(tenant);

        await context.SaveChangesAsync();

        tenant.CreatedAt.Should().NotBe(default);
        tenant.CreatedBy.Should().Be(userId.ToString());
    }

    [Fact]
    public async Task SaveChanges_WhenAggregateHasDomainEvents_WritesThemToOutboxAndClearsThem()
    {
        await using var context = TestDbContextFactory.Create(fixture.SuperuserConnectionString);
        var tenant = Tenant.Create("Tenant com evento", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;

        tenant.DomainEvents.Should().NotBeEmpty("Tenant.Create levanta TenantCreatedEvent");

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        tenant.DomainEvents.Should().BeEmpty("o OutboxInterceptor deve limpar os eventos do agregado após gravá-los");

        var outboxMessage = await context.OutboxMessages.FirstOrDefaultAsync(m => m.AggregateId == tenant.Id.Value);

        outboxMessage.Should().NotBeNull();
        outboxMessage!.EventType.Should().Contain("TenantCreatedEvent");
        outboxMessage.Status.Should().Be(OutboxStatus.Pending);
    }

    [Fact]
    public async Task Query_WhenTenantContextIsSet_AppliesGlobalTenantFilterToUsers()
    {
        var tenantContext = new AsyncLocalTenantContext();
        await using var context = TestDbContextFactory.Create(fixture.SuperuserConnectionString, tenantContext);

        var tenantA = Tenant.Create("Tenant A - filtro global", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;
        var tenantB = Tenant.Create("Tenant B - filtro global", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;
        context.Tenants.AddRange(tenantA, tenantB);
        await context.SaveChangesAsync();

        var userA = User.Create(UserId.New(), tenantA.Id.Value, "Usuário A", Email.Create($"{Guid.NewGuid()}@teste.com").Value, UserRole.Lawyer).Value;
        var userB = User.Create(UserId.New(), tenantB.Id.Value, "Usuário B", Email.Create($"{Guid.NewGuid()}@teste.com").Value, UserRole.Lawyer).Value;
        context.Users.AddRange(userA, userB);
        await context.SaveChangesAsync();

        tenantContext.SetTenant(tenantA.Id.Value);

        var visibleUsers = await context.Users.ToListAsync();

        visibleUsers.Should().ContainSingle(u => u.Id == userA.Id);
        visibleUsers.Should().NotContain(u => u.Id == userB.Id);
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;

        public Guid? TenantId => null;

        public string? Email => null;

        public IReadOnlyCollection<string> Roles { get; } = [];

        public bool IsInRole(string role) => false;
    }
}
