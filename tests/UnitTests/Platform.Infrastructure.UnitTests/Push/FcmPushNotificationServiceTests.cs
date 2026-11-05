using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Devices;
using Advocacia.Platform.Infrastructure.Push;
using FirebaseAdmin.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Advocacia.Platform.Infrastructure.UnitTests.Push;

public class FcmPushNotificationServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private const string SecretPrefix = "segredo-de-entrega-do-aparelho-";

    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFcmSender _sender = Substitute.For<IFcmSender>();
    private readonly CapturingLogger<FcmPushNotificationService> _logger = new();

    private static Device NewDevice(string suffix) =>
        Device.Register(TenantId, UserId, DevicePlatform.Android, SecretPrefix + suffix, Now).Value;

    private FcmPushNotificationService CreateSut() => new(_devices, _unitOfWork, _sender, _logger);

    private static PushMessage Message() => new(TenantId, UserId, "Nova publicação", "Processo 123", "/dashboard");

    [Fact]
    public async Task SendToUser_SendsToEveryActiveDevice()
    {
        _devices.ListActiveForUserAsync(TenantId, UserId, Arg.Any<CancellationToken>()).Returns([NewDevice("a"), NewDevice("b")]);
        _sender.SendAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>()).Returns(FcmSendOutcome.Sent);

        var result = await CreateSut().SendToUserAsync(Message());

        result.Should().Be(new PushSendResult(2, 0, 0));
        await _sender.Received(2).SendAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToUser_AsksOnlyForActiveDevices_SoRevokedOnesNeverGetPush()
    {
        _devices.ListActiveForUserAsync(TenantId, UserId, Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateSut().SendToUserAsync(Message());

        result.Should().Be(PushSendResult.None);
        await _devices.DidNotReceiveWithAnyArgs().ListForUserAsync(default, default, default);
        await _sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task SendToUser_RemovesDevicesTheFcmNoLongerKnows_AndSaves()
    {
        var gone = NewDevice("gone");
        var fine = NewDevice("fine");
        _devices.ListActiveForUserAsync(TenantId, UserId, Arg.Any<CancellationToken>()).Returns([gone, fine]);
        _sender.SendAsync(Arg.Is<Message>(m => m.Token == gone.DeviceToken), Arg.Any<CancellationToken>()).Returns(FcmSendOutcome.Unregistered);
        _sender.SendAsync(Arg.Is<Message>(m => m.Token == fine.DeviceToken), Arg.Any<CancellationToken>()).Returns(FcmSendOutcome.Sent);

        var result = await CreateSut().SendToUserAsync(Message());

        result.Should().Be(new PushSendResult(1, 0, 1));
        _devices.Received(1).Remove(gone);
        _devices.DidNotReceive().Remove(fine);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToUser_AFailureOnOneDevice_DoesNotStopTheOthers()
    {
        var bad = NewDevice("bad");
        var good = NewDevice("good");
        _devices.ListActiveForUserAsync(TenantId, UserId, Arg.Any<CancellationToken>()).Returns([bad, good]);
        _sender.SendAsync(Arg.Is<Message>(m => m.Token == bad.DeviceToken), Arg.Any<CancellationToken>()).Returns(FcmSendOutcome.Failed);
        _sender.SendAsync(Arg.Is<Message>(m => m.Token == good.DeviceToken), Arg.Any<CancellationToken>()).Returns(FcmSendOutcome.Sent);

        var result = await CreateSut().SendToUserAsync(Message());

        result.Should().Be(new PushSendResult(1, 1, 0));
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default); // nada removido, nada a salvar
    }

    [Fact]
    public async Task SendToUser_NeverWritesTheDeliveryTokenToTheLogs()
    {
        _devices.ListActiveForUserAsync(TenantId, UserId, Arg.Any<CancellationToken>())
            .Returns([NewDevice("falha"), NewDevice("removido")]);
        _sender.SendAsync(Arg.Is<Message>(m => m.Token!.EndsWith("falha")), Arg.Any<CancellationToken>()).Returns(FcmSendOutcome.Failed);
        _sender.SendAsync(Arg.Is<Message>(m => m.Token!.EndsWith("removido")), Arg.Any<CancellationToken>()).Returns(FcmSendOutcome.Unregistered);

        await CreateSut().SendToUserAsync(Message());

        _logger.Entries.Should().NotBeEmpty();
        _logger.Entries.Should().NotContain(e => e.Contains(SecretPrefix), "o token de entrega é um segredo (LGPD)");
    }

    [Fact]
    public void BuildMessage_CarriesTitleBodyAndRouteForTheDeepLink()
    {
        var device = NewDevice("x");

        var message = FcmPushNotificationService.BuildMessage(device, Message());

        message.Notification.Title.Should().Be("Nova publicação");
        message.Notification.Body.Should().Be("Processo 123");
        message.Data.Should().ContainKey("route").WhoseValue.Should().Be("/dashboard");
        message.Android.Notification.ChannelId.Should().Be("alertas"); // o canal criado pelo app
    }

    [Fact]
    public void BuildMessage_WithoutARoute_SendsNoData()
    {
        var message = FcmPushNotificationService.BuildMessage(NewDevice("x"), Message() with { Route = null });

        message.Data.Should().BeNull();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(formatter(state, exception) + " " + exception?.Message);
    }
}
