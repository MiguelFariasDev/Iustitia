using System.Net;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Domain.Identity;
using Advocacia.Platform.Infrastructure.Notifications.EmailService;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Advocacia.Platform.Infrastructure.UnitTests.Notifications;

public sealed class EmailTests
{
    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status);
        }
    }

    private static readonly EmailMessage Message = new("ana@escritorio.com.br", "Assunto", "<p>oi</p>", "oi");

    private static ResendEmailSender Sender(RecordingHandler handler, EmailOptions options) =>
        new(new HttpClient(handler), Options.Create(options), NullLogger<ResendEmailSender>.Instance);

    private static EmailOptions Configured() => new() { ApiKey = "re_test", FromAddress = "avisos@iustitia.app" };

    [Fact]
    public async Task Send_PostsToTheProviderWithBearerKeyAndSender()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        await Sender(handler, Configured()).SendAsync(Message);

        handler.Request!.RequestUri!.ToString().Should().Be("https://api.resend.com/emails");
        handler.Request.Headers.Authorization!.Parameter.Should().Be("re_test");
        using var body = System.Text.Json.JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("from").GetString().Should().Be("Iustitia <avisos@iustitia.app>");
        body.RootElement.GetProperty("to")[0].GetString().Should().Be("ana@escritorio.com.br");
        body.RootElement.GetProperty("subject").GetString().Should().Be("Assunto");
    }

    [Fact]
    public async Task Send_WithoutKeyOrSender_DoesNotCallTheProvider()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        await Sender(handler, new EmailOptions()).SendAsync(Message);

        handler.Request.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Send_OnAnErrorStatus_ThrowsWithoutLeakingTheRecipient(HttpStatusCode status)
    {
        var act = async () => await Sender(new RecordingHandler(status), Configured()).SendAsync(Message);

        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Which.Message.Should().NotContain("ana@").And.Contain(((int)status).ToString());
        exception.Which.StatusCode.Should().Be(status);
    }

    [Fact]
    public void Template_EscapesTheTitleAndKeepsOnlyTitleAndLink()
    {
        var (subject, html, text) = EmailTemplates.Alert("Prazo <script>alert(1)</script> & cia", "https://app.iustitia.app/alertas");

        html.Should().NotContain("<script>").And.Contain("&lt;script&gt;").And.Contain("https://app.iustitia.app/alertas");
        subject.Should().StartWith("Iustitia: ");
        text.Should().Contain("https://app.iustitia.app/alertas");
    }

    private static User ActiveUser(Guid tenantId)
    {
        var user = User.Create(UserId.From(Guid.NewGuid()), tenantId, "Ana", Email.Create("ana@escritorio.com.br").Value, UserRole.Lawyer).Value;
        user.Activate();

        return user;
    }

    private static (EmailService Service, IEmailSender Sender, IRepository<User, UserId> Users, ITenantContext Tenant) Build(EmailOptions? options = null)
    {
        var sender = Substitute.For<IEmailSender>();
        var users = Substitute.For<IRepository<User, UserId>>();
        var tenant = Substitute.For<ITenantContext>();
        var service = new EmailService(sender, users, tenant, Options.Create(options ?? Configured()), NullLogger<EmailService>.Instance);

        return (service, sender, users, tenant);
    }

    [Fact]
    public async Task Alert_OfAConfiguredType_SendsToTheUserInTheEventsTenant()
    {
        var (service, sender, users, tenant) = Build();
        var tenantId = Guid.NewGuid();
        var user = ActiveUser(tenantId);
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await service.SendAlertEmailAsync(user.Id.Value, tenantId, "Publication", "Nova publicação");

        tenant.Received().SetTenant(tenantId);
        await sender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == "ana@escritorio.com.br" && m.Subject.Contains("Nova publicação")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Alert_OfATypeNotConfigured_SendsNothing()
    {
        var (service, sender, users, _) = Build();

        await service.SendAlertEmailAsync(Guid.NewGuid(), Guid.NewGuid(), "Birthday", "Aniversário");

        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
        await users.DidNotReceiveWithAnyArgs().GetByIdAsync(default!, default);
    }

    [Fact]
    public async Task Alert_ForAnInactiveOrMissingUser_SendsNothing()
    {
        var (service, sender, users, _) = Build();
        var invited = User.Invite(UserId.From(Guid.NewGuid()), Guid.NewGuid(), "Bia", Email.Create("bia@x.com.br").Value, UserRole.Intern).Value;
        users.GetByIdAsync(invited.Id, Arg.Any<CancellationToken>()).Returns(invited);

        await service.SendAlertEmailAsync(invited.Id.Value, Guid.NewGuid(), "Task", "Prazo");
        await service.SendAlertEmailAsync(Guid.NewGuid(), Guid.NewGuid(), "Task", "Prazo");

        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }
}
