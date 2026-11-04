using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Advocacia.Modules.Legal.IntegrationTests.Common;

/// <summary>
/// DJEN de mentira: um <see cref="HttpMessageHandler"/> que responde no formato real da API de
/// comunicações do CNJ, com paginação. Usado no lugar de um WireMock para exercitar o
/// <c>DjenClient</c> REAL (parsing, query string, mapeamento de erro) sem abrir socket nem
/// porta — assim os testes rodam em paralelo sem conflito de porta e sem depender de rede.
///
/// Registra cada página pedida, para os testes poderem afirmar sobre o comportamento de
/// paginação do job.
/// </summary>
internal sealed class FakeDjenServer : HttpMessageHandler
{
    private readonly List<FakeDjenItem> _items = [];
    private HttpStatusCode? _failWithStatus;
    private int _failAfterRequests = int.MaxValue;
    private bool _lieAboutTotalCount;

    internal List<int> RequestedPages { get; } = [];

    internal List<string?> RequestedProcesses { get; } = [];

    internal int RequestCount => RequestedPages.Count;

    internal FakeDjenServer WithItems(params FakeDjenItem[] items)
    {
        _items.AddRange(items);
        return this;
    }

    /// <summary>Faz o "CNJ" cair a partir da N-ésima requisição (1-based), simulando indisponibilidade no meio da captura.</summary>
    internal FakeDjenServer FailingFromRequest(int requestNumber, HttpStatusCode statusCode = HttpStatusCode.ServiceUnavailable)
    {
        _failAfterRequests = requestNumber;
        _failWithStatus = statusCode;
        return this;
    }

    /// <summary>
    /// Reproduz o defeito real do DJEN medido na Etapa 1.3: com itensPorPagina=100 a API
    /// devolve `count` igual ao tamanho da página em vez do total. Confiar nesse valor fazia
    /// a captura parar na página 1.
    /// </summary>
    internal FakeDjenServer ReportingPageSizeAsTotalCount()
    {
        _lieAboutTotalCount = true;
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
        var page = int.Parse(query["pagina"] ?? "1");
        var pageSize = int.Parse(query["itensPorPagina"] ?? "100");
        var tribunal = query["siglaTribunal"];

        RequestedPages.Add(page);
        RequestedProcesses.Add(query["numeroProcesso"]);

        if (_failWithStatus is { } statusCode && RequestCount >= _failAfterRequests)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }

        var numeroProcesso = query["numeroProcesso"];

        var matching = _items
            .Where(item => tribunal is null || item.Tribunal == tribunal)
            // O DJEN casa pelo número SEM máscara — o fake replica isso para os testes
            // pegarem se o cliente esquecer de tirar a pontuação.
            .Where(item => numeroProcesso is null || Unmask(item.CnjNumber) == numeroProcesso)
            .ToList();

        var pageItems = matching.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var payload = new
        {
            status = "success",
            message = string.Empty,
            count = _lieAboutTotalCount && pageItems.Count >= pageSize ? pageSize : matching.Count,
            items = pageItems.Select(item => new
            {
                id = item.Id,
                hash = $"hash-{item.Id}",
                data_disponibilizacao = item.PublishedOn.ToString("yyyy-MM-dd"),
                siglaTribunal = item.Tribunal,
                tipoComunicacao = "Intimação",
                nomeOrgao = "1ª Vara Cível",
                texto = item.Text,
                numeroprocessocommascara = item.CnjNumber,
                meio = "D",
            }),
        };

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        });
    }

    private static string Unmask(string cnjNumber) =>
        new(cnjNumber.Where(char.IsDigit).ToArray());
}

/// <param name="Id">Id da comunicação no CNJ — vira o ExternalId e a chave de deduplicação.</param>
internal sealed record FakeDjenItem(
    long Id,
    string CnjNumber,
    string Tribunal,
    DateOnly PublishedOn,
    string Text = "Fica a parte intimada a manifestar-se no prazo de 15 dias.");
