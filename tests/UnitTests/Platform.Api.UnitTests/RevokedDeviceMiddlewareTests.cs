using System.Text.Json;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Api.Middleware;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Devices;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Advocacia.Platform.Api.UnitTests;

public class RevokedDeviceMiddlewareTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _deviceId = Guid.NewGuid();

    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private bool _nextCalled;

    public RevokedDeviceMiddlewareTests()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.TenantId.Returns(_tenantId);
    }

    private RevokedDeviceMiddleware CreateSut() => new(
        _ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        },
        _cache);

    private static DefaultHttpContext Context(string? deviceHeader)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        // O resultado ProblemDetails resolve um ILoggerFactory ao ser executado.
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        context.RequestServices = services.BuildServiceProvider();
        if (deviceHeader is not null)
        {
            context.Request.Headers[RevokedDeviceMiddleware.HeaderName] = deviceHeader;
        }

        return context;
    }

    private void DeviceIs(bool revoked) =>
        _devices.IsRevokedAsync(_tenantId, DeviceId.From(_deviceId), Arg.Any<CancellationToken>()).Returns(revoked);

    [Fact]
    public async Task ARevokedDevice_Gets401DeviceRevoked_AndTheRequestDoesNotContinue()
    {
        DeviceIs(true);
        var context = Context(_deviceId.ToString());

        await CreateSut().InvokeAsync(context, _currentUser, _devices);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextCalled.Should().BeFalse();

        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        body.RootElement.GetProperty("errorCode").GetString().Should().Be("DEVICE_REVOKED");
    }

    [Fact]
    public async Task AnActiveDevice_PassesThrough()
    {
        DeviceIs(false);

        await CreateSut().InvokeAsync(Context(_deviceId.ToString()), _currentUser, _devices);

        _nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("isto-nao-e-um-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task WithoutAUsableHeader_PassesThroughWithoutTouchingTheDatabase(string? header)
    {
        await CreateSut().InvokeAsync(Context(header), _currentUser, _devices);

        _nextCalled.Should().BeTrue();
        await _devices.DidNotReceiveWithAnyArgs().IsRevokedAsync(default, default, default);
    }

    [Fact]
    public async Task AnAnonymousRequest_IsNotChecked()
    {
        _currentUser.IsAuthenticated.Returns(false);
        DeviceIs(true);

        await CreateSut().InvokeAsync(Context(_deviceId.ToString()), _currentUser, _devices);

        _nextCalled.Should().BeTrue();
        await _devices.DidNotReceiveWithAnyArgs().IsRevokedAsync(default, default, default);
    }

    [Fact]
    public async Task TheLookupIsCached_SoTheDatabaseIsNotHitOnEveryRequest()
    {
        DeviceIs(false);
        var sut = CreateSut();

        await sut.InvokeAsync(Context(_deviceId.ToString()), _currentUser, _devices);
        await sut.InvokeAsync(Context(_deviceId.ToString()), _currentUser, _devices);
        await sut.InvokeAsync(Context(_deviceId.ToString()), _currentUser, _devices);

        await _devices.Received(1).IsRevokedAsync(_tenantId, DeviceId.From(_deviceId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheCacheIsPerTenant_SoAnotherOfficesVerdictIsNeverReused()
    {
        DeviceIs(true);
        await CreateSut().InvokeAsync(Context(_deviceId.ToString()), _currentUser, _devices); // tenant A: revogado

        var otherTenant = Guid.NewGuid();
        _currentUser.TenantId.Returns(otherTenant);
        _devices.IsRevokedAsync(otherTenant, DeviceId.From(_deviceId), Arg.Any<CancellationToken>()).Returns(false);
        _nextCalled = false;

        await CreateSut().InvokeAsync(Context(_deviceId.ToString()), _currentUser, _devices);

        _nextCalled.Should().BeTrue();
    }
}
