using System.Net;
using Advocacia.Platform.Infrastructure.Auth;
using Advocacia.Platform.Infrastructure.Realtime;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Platform.Infrastructure.UnitTests.Realtime;

public sealed class SupabaseRealtimeNotifierTests
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

    private static SupabaseRealtimeNotifier Create(RecordingHandler handler, string url = "https://x.supabase.co/", string key = "service-key") =>
        new(new HttpClient(handler), Options.Create(new SupabaseOptions { Url = url, ServiceRoleKey = key }),
            NullLogger<SupabaseRealtimeNotifier>.Instance);

    [Fact]
    public async Task Broadcast_PostsAPrivateMessageToTheRealtimeApi()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);

        await Create(handler).BroadcastAsync("tenant:t:alerts:u", "alert.created", new { title = "x" });

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.ToString().Should().Be("https://x.supabase.co/realtime/v1/api/broadcast");
        handler.Request.Headers.GetValues("apikey").Should().ContainSingle("service-key");
        handler.Request.Headers.Authorization!.Parameter.Should().Be("service-key");
        handler.Body.Should().Contain("\"topic\":\"tenant:t:alerts:u\"").And.Contain("\"event\":\"alert.created\"").And.Contain("\"private\":true");
    }

    [Fact]
    public async Task Broadcast_WithoutSupabaseConfigured_IsANoOp()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);

        await Create(handler, url: "", key: "").BroadcastAsync("c", "e", new { });

        handler.Request.Should().BeNull();
    }

    [Fact]
    public async Task Broadcast_OnAnErrorStatus_Throws()
    {
        var act = async () => await Create(new RecordingHandler(HttpStatusCode.Unauthorized)).BroadcastAsync("c", "e", new { });

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
