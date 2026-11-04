using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Platform.Domain.Tenancy;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;

namespace Advocacia.Platform.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class RepositoryTests(PostgresFixture fixture)
{
    private sealed class ActiveTenantsPagedSpecification : Specification<Tenant>
    {
        public ActiveTenantsPagedSpecification(string namePrefix, int skip, int take)
            : base(t => t.Status == TenantStatus.Active && t.Name.StartsWith(namePrefix))
        {
            ApplyOrderBy(t => t.Name);
            ApplyPaging(skip, take);
        }
    }

    [Fact]
    public async Task ListAsync_WithSpecification_FiltersOrdersAndPaginates()
    {
        await using var context = TestDbContextFactory.Create(fixture.SuperuserConnectionString);
        var repository = new Repository<Tenant, TenantId>(context);

        var prefix = $"Repo-{Guid.NewGuid():N}";
        for (var i = 0; i < 5; i++)
        {
            var tenant = Tenant.Create($"{prefix}-{i:00}", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;
            repository.Add(tenant);
        }

        await context.SaveChangesAsync();

        var firstPage = await repository.ListAsync(new ActiveTenantsPagedSpecification(prefix, 0, 2));
        var secondPage = await repository.ListAsync(new ActiveTenantsPagedSpecification(prefix, 2, 2));

        firstPage.Should().HaveCount(2);
        secondPage.Should().HaveCount(2);
        firstPage.Select(t => t.Name).Should().NotIntersectWith(secondPage.Select(t => t.Name));
        firstPage.Select(t => t.Name).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetByIdAsync_WhenEntitySoftDeleted_ReturnsNull()
    {
        await using var context = TestDbContextFactory.Create(fixture.SuperuserConnectionString);
        var repository = new Repository<Tenant, TenantId>(context);

        var tenant = Tenant.Create("Repo soft delete", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;
        repository.Add(tenant);
        await context.SaveChangesAsync();

        repository.Remove(tenant);
        await context.SaveChangesAsync();

        var found = await repository.GetByIdAsync(tenant.Id);

        found.Should().BeNull("o repositório genérico deve respeitar o filtro global de soft delete");
    }

    [Fact]
    public async Task GetByIdAsync_WhenEntityExists_ReturnsIt()
    {
        await using var context = TestDbContextFactory.Create(fixture.SuperuserConnectionString);
        var repository = new Repository<Tenant, TenantId>(context);

        var tenant = Tenant.Create("Repo get by id", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;
        repository.Add(tenant);
        await context.SaveChangesAsync();

        var found = await repository.GetByIdAsync(tenant.Id);

        found.Should().NotBeNull();
        found!.Id.Should().Be(tenant.Id);
    }

    [Fact]
    public async Task CountAsync_RespectsSpecificationCriteria()
    {
        await using var context = TestDbContextFactory.Create(fixture.SuperuserConnectionString);
        var repository = new Repository<Tenant, TenantId>(context);

        var prefix = $"Count-{Guid.NewGuid():N}";
        repository.Add(Tenant.Create($"{prefix}-A", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value);
        repository.Add(Tenant.Create($"{prefix}-B", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value);
        await context.SaveChangesAsync();

        var count = await repository.CountAsync(new ActiveTenantsPagedSpecification(prefix, 0, 100));

        count.Should().Be(2);
    }
}
