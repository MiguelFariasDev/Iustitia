using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Infrastructure.Push;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Advocacia.Platform.Infrastructure.UnitTests.Push;

public class NoopAndOptionsTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("", "  ", false)]
    [InlineData("{}", null, true)]
    [InlineData(null, "/run/secrets/fcm.json", true)]
    public void PushOptions_IsConfiguredOnlyWithSomeCredential(string? json, string? path, bool expected)
    {
        new PushOptions { CredentialsJson = json, CredentialsPath = path }.IsConfigured.Should().Be(expected);
    }

    [Fact]
    public async Task Noop_SendsNothingAndNeverThrows()
    {
        var sut = new NoopPushNotificationService(NullLogger<NoopPushNotificationService>.Instance);

        var result = await sut.SendToUserAsync(new PushMessage(Guid.NewGuid(), Guid.NewGuid(), "t", "b"));

        result.Should().Be(PushSendResult.None);
    }

    [Fact]
    public void WithoutCredentials_TheNoopIsRegistered_NotFcm()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPushNotifications(new PushOptions());

        services.Should().Contain(d => d.ServiceType == typeof(IPushNotificationService)
            && d.ImplementationType == typeof(NoopPushNotificationService));
        services.Should().NotContain(d => d.ServiceType == typeof(IFcmSender));
    }
}
