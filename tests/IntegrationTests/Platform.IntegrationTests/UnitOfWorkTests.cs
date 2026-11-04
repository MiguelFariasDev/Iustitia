using Advocacia.BuildingBlocks.Infrastructure.Persistence;
using Advocacia.Platform.Domain.Tenancy;
using Advocacia.Platform.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Platform.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class UnitOfWorkTests(PostgresFixture fixture)
{
    [Fact]
    public async Task CommitTransactionAsync_PersistsChanges()
    {
        await using var context = TestDbContextFactory.Create(fixture.SuperuserConnectionString);
        await using var unitOfWork = new UnitOfWork(context);

        var tenant = Tenant.Create("UoW commit", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;

        await unitOfWork.BeginTransactionAsync();
        context.Tenants.Add(tenant);
        await unitOfWork.CommitTransactionAsync();

        await using var readContext = TestDbContextFactory.Create(fixture.SuperuserConnectionString);
        var found = await readContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.Id);

        found.Should().NotBeNull();
    }

    [Fact]
    public async Task RollbackTransactionAsync_DiscardsChanges()
    {
        await using var context = TestDbContextFactory.Create(fixture.SuperuserConnectionString);
        await using var unitOfWork = new UnitOfWork(context);

        var tenant = Tenant.Create("UoW rollback", CNPJ.Create(TestCnpjGenerator.Generate()).Value).Value;

        await unitOfWork.BeginTransactionAsync();
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        await unitOfWork.RollbackTransactionAsync();

        await using var readContext = TestDbContextFactory.Create(fixture.SuperuserConnectionString);
        var found = await readContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.Id);

        found.Should().BeNull("o rollback deve descartar as mudanças feitas dentro da transação");
    }
}
