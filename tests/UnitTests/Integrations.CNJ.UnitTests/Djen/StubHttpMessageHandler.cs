using System.Net;
using System.Text;

namespace Advocacia.Modules.Integrations.CNJ.UnitTests.Djen;

/// <summary>
/// Handler de teste que responde a partir de uma fila de respostas pré-programadas e registra
/// as URIs recebidas. Preferido a um servidor HTTP de mentira (WireMock): o que se quer
/// validar aqui é como o DjenClient monta a query string e interpreta corpo/status, o que não
/// precisa de socket nem de porta — e sem porta os testes rodam em paralelo sem conflito.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    internal List<Uri> ReceivedUris { get; } = [];

    internal StubHttpMessageHandler RespondWithJson(HttpStatusCode statusCode, string json)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        return this;
    }

    internal StubHttpMessageHandler RespondWithStatus(HttpStatusCode statusCode)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode));
        return this;
    }

    internal StubHttpMessageHandler Throw(Exception exception)
    {
        _responses.Enqueue(_ => throw exception);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ReceivedUris.Add(request.RequestUri!);

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"StubHttpMessageHandler recebeu uma requisição inesperada para {request.RequestUri} (fila de respostas vazia).");
        }

        return Task.FromResult(_responses.Dequeue()(request));
    }
}
