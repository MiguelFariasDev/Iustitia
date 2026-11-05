using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Platform.Domain.Devices;
using Advocacia.Platform.Infrastructure.Persistence;
using Advocacia.Platform.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Platform.Infrastructure.UnitTests.Persistence;

/// <summary>Testes de dispositivos contra o PostgreSQL de verdade (variável <c>ADVOCACIA_TEST_DB</c>); sem ela, são PULADOS.</summary>
public sealed class RequiresDatabaseFactAttribute : FactAttribute
{
    public RequiresDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ADVOCACIA_TEST_DB")))
        {
            Skip = "Defina ADVOCACIA_TEST_DB com a connection string de um Postgres migrado.";
        }
    }
}

public sealed class DevicePostgresTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private readonly PlatformDbContext? _db;

    public DevicePostgresTests()
    {
        var connection = Environment.GetEnvironmentVariable("ADVOCACIA_TEST_DB");
        if (string.IsNullOrWhiteSpace(connection))
        {
            return;
        }

        var tenantContext = new AsyncLocalTenantContext();
        tenantContext.SetTenant(_tenant);
        _db = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>().UseNpgsql(connection).UseSnakeCaseNamingConvention().Options, tenantContext);
    }

    public void Dispose() => _db?.Dispose();

    private Device Add(string token, bool revoked = false, Guid? tenant = null)
    {
        var device = Device.Register(tenant ?? _tenant, _user, DevicePlatform.Android, "token-pg-" + token + "-xxxxxxxxxxxx", Now).Value;
        if (revoked)
        {
            device.Revoke(Now);
        }

        _db!.Devices.Add(device);
        _db.SaveChanges();
        return device;
    }

    [RequiresDatabaseFact]
    public async Task RepositoryQueries_TranslateToValidSql()
    {
        var active = Add("a");
        var revoked = Add("r", revoked: true);
        var repo = new DeviceRepository(_db!);

        (await repo.ListForUserAsync(_tenant, _user)).Should().HaveCount(2);
        (await repo.ListActiveForUserAsync(_tenant, _user)).Should().ContainSingle().Which.Id.Should().Be(active.Id);
        (await repo.IsRevokedAsync(_tenant, revoked.Id)).Should().BeTrue();
        (await repo.IsRevokedAsync(_tenant, active.Id)).Should().BeFalse();
        (await repo.GetByTokenAsync(_tenant, active.DeviceToken)).Should().NotBeNull();
        (await repo.GetForUserAsync(_tenant, Guid.NewGuid(), active.Id)).Should().BeNull();
    }

    [RequiresDatabaseFact]
    public void TheDatabaseEnforcesOneTokenPerTenant()
    {
        var first = Add("unico");

        var act = () => Add("unico"); // mesmo token, mesmo escritório

        act.Should().Throw<DbUpdateException>();
        first.Should().NotBeNull();
    }

    [RequiresDatabaseFact]
    public void TheSameTokenIsAllowedInAnotherTenant()
    {
        Add("compartilhado");

        var act = () => Add("compartilhado", tenant: Guid.NewGuid());

        act.Should().NotThrow();
    }
}
