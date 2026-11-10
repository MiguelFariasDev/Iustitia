using Advocacia.Modules.Drafting.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.Ollama;

/// <summary>
/// Endereço atual do Ollama. Começa em <c>Ollama:BaseUrl</c>; se a Vast.ai estiver configurada para
/// descobrir o endereço (IP e porta mudam a cada subida), o orquestrador o atualiza aqui. Todo
/// endereço passa pelo <see cref="TransportGuard"/>: http:// para host público só com opt-in explícito.
/// </summary>
public sealed class OllamaEndpoint(IOptions<OllamaOptions> options) : IOllamaEndpoint
{
    private readonly object _gate = new();
    private Uri? _current;

    public Uri BaseAddress
    {
        get
        {
            lock (_gate)
            {
                return _current ??= Validate(options.Value.BaseUrl);
            }
        }
    }

    public void Set(Uri baseAddress)
    {
        var validated = Validate(baseAddress.ToString());
        lock (_gate)
        {
            _current = validated;
        }
    }

    private Uri Validate(string url)
    {
        var o = options.Value;
        var problem = TransportGuard.Validate("Ollama", url, o.ApiKey, o.AllowInsecureHttp);
        if (problem is not null)
        {
            throw new InvalidOperationException(problem);
        }

        // A barra final garante que "api/chat" seja relativo ao endereço, e não substitua um caminho-base.
        return new Uri(url.EndsWith('/') ? url : url + "/");
    }
}
