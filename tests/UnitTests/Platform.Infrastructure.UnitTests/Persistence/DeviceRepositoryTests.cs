using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Platform.Domain.Devices;
using Advocacia.Platform.Infrastructure.Persistence;
using Advocacia.Platform.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Platform.Infrastructure.UnitTests.Persistence;

/// <summary>
/// Prova o isolamento FEITO PELA APLICAÇÃO (filtro por tenant e por usuário). O RLS do Postgres é
/// outra camada e é provado no banco real (014_devices.sql) — o EF em memória não o executa.
/// </summary>
public sealed class DeviceRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _ana = Guid.NewGuid();
    private readonly Guid _bruno = Guid.NewGuid();

    private readonly PlatformDbContext _db;
    private readonly DeviceRepository _repo;
    private readonly AsyncLocalTenantContext _tenant = new();

    public DeviceRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _tenant.SetTenant(_tenantA);
        _db = new PlatformDbContext(options, _tenant);
        _repo = new DeviceRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private Device Seed(Guid tenant, Guid user, string token, bool revoked = false, int minutesAgo = 0)
    {
        var device = Device.Register(tenant, user, DevicePlatform.Android, "token-de-teste-" + token + "-xxxxxxxx", Now.AddMinutes(-minutesAgo)).Value;
        if (revoked)
        {
            device.Revoke(Now);
        }

        _db.Devices.Add(device);
        _db.SaveChanges();
        return device;
    }

    [Fact]
    public async Task ListForUser_ReturnsOnlyThatUsersDevices_NewestFirst_RevokedIncluded()
    {
        var older = Seed(_tenantA, _ana, "a1", minutesAgo: 60);
        var newer = Seed(_tenantA, _ana, "a2", minutesAgo: 5);
        var revoked = Seed(_tenantA, _ana, "a3", revoked: true, minutesAgo: 30);
        Seed(_tenantA, _bruno, "b1");

        var list = await _repo.ListForUserAsync(_tenantA, _ana);

        list.Select(d => d.Id).Should().Equal(newer.Id, revoked.Id, older.Id);
    }

    [Fact]
    public async Task ListActiveForUser_ExcludesRevoked_SoTheyNeverReceivePush()
    {
        var active = Seed(_tenantA, _ana, "a1");
        Seed(_tenantA, _ana, "a2", revoked: true);

        var list = await _repo.ListActiveForUserAsync(_tenantA, _ana);

        list.Should().ContainSingle().Which.Id.Should().Be(active.Id);
    }

    [Fact]
    public async Task GetForUser_DoesNotReturnAColleaguesDevice()
    {
        var brunos = Seed(_tenantA, _bruno, "b1");

        (await _repo.GetForUserAsync(_tenantA, _ana, brunos.Id)).Should().BeNull();
        (await _repo.GetForUserAsync(_tenantA, _bruno, brunos.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task TheTenantFilterHidesOtherOfficesDevices_EvenWhenAskingByTheirIds()
    {
        var other = Seed(_tenantB, _ana, "x1"); // mesmo UserId, outro escritório
        _tenant.SetTenant(_tenantA);

        (await _repo.GetForUserAsync(_tenantA, _ana, other.Id)).Should().BeNull();
        (await _repo.ListForUserAsync(_tenantA, _ana)).Should().BeEmpty();
        (await _repo.IsRevokedAsync(_tenantA, other.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task GetByToken_FindsAColleaguesRow_ToReassignASharedDevice()
    {
        var brunos = Seed(_tenantA, _bruno, "shared");

        var found = await _repo.GetByTokenAsync(_tenantA, brunos.DeviceToken);

        found.Should().NotBeNull();
        found!.UserId.Should().Be(_bruno);
    }

    [Fact]
    public async Task IsRevoked_IsTrueOnlyForARevokedDeviceOfTheTenant()
    {
        var revoked = Seed(_tenantA, _ana, "r", revoked: true);
        var active = Seed(_tenantA, _ana, "ok");

        (await _repo.IsRevokedAsync(_tenantA, revoked.Id)).Should().BeTrue();
        (await _repo.IsRevokedAsync(_tenantA, active.Id)).Should().BeFalse();
        // Id desconhecido NÃO é "revogado": não se tranca para fora quem manda um id que não conhecemos.
        (await _repo.IsRevokedAsync(_tenantA, DeviceId.New())).Should().BeFalse();
    }
}
