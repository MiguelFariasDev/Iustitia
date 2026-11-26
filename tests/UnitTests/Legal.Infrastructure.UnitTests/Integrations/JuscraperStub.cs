using System.Net;
using System.Text;

namespace Advocacia.Modules.Legal.Infrastructure.UnitTests.Integrations;

/// <summary>Servidor juscraper falso: responde em fila e guarda os caminhos pedidos.</summary>
internal sealed class JuscraperStub : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _respostas = new();

    internal List<string> Caminhos { get; } = [];

    internal List<string?> Chaves { get; } = [];

    internal JuscraperStub Json(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _respostas.Enqueue(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        return this;
    }

    internal JuscraperStub Problema(HttpStatusCode status, string codigo) =>
        Json($$"""{"title":"{{codigo}}","status":{{(int)status}},"codigo":"{{codigo}}","tribunal":"TJSP","transitorio":false}""", status);

    internal JuscraperStub Status(HttpStatusCode status)
    {
        _respostas.Enqueue(_ => new HttpResponseMessage(status));
        return this;
    }

    internal JuscraperStub Lanca(Exception exception)
    {
        _respostas.Enqueue(_ => throw exception);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Caminhos.Add(request.RequestUri!.PathAndQuery);
        Chaves.Add(request.Headers.TryGetValues("X-Api-Key", out var valores) ? valores.Single() : null);

        if (_respostas.Count == 0)
        {
            throw new InvalidOperationException($"Requisição inesperada ao juscraper: {request.RequestUri}");
        }

        return Task.FromResult(_respostas.Dequeue()(request));
    }
}

internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("http://juscraper.test/") };
}
