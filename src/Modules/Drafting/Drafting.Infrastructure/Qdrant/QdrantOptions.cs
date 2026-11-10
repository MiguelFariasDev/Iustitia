using Advocacia.Modules.Drafting.Infrastructure.Ollama;

namespace Advocacia.Modules.Drafting.Infrastructure.Qdrant;

/// <summary>
/// Seção <c>Qdrant</c> (só a conexão; nome da coleção e limites ficam em <c>Rag</c>). O cliente
/// oficial fala gRPC: a porta da URL é a do gRPC (6334 por padrão no Qdrant), não a REST (6333).
/// URL e chave vêm de <c>QDRANT__BASEURL</c> / <c>QDRANT__APIKEY</c>.
///
/// RECOMENDAÇÃO: hospede o Qdrant num servidor SEMPRE LIGADO (e não na instância de GPU que é
/// parada por economia): a busca vetorial não usa GPU, e os vetores precisam existir com a GPU desligada.
/// </summary>
public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string BaseUrl { get; set; } = "http://localhost:6334";

    public string? ApiKey { get; set; }

    public int Dimensoes { get; set; } = 1024;

    public int TimeoutSegundos { get; set; } = 30;

    /// <summary>Ver <see cref="OllamaOptions.AllowInsecureHttp"/>: mesma regra, mesmo motivo.</summary>
    public bool AllowInsecureHttp { get; set; }

    public string? Validate() => TransportGuard.Validate("Qdrant", BaseUrl, ApiKey, AllowInsecureHttp);
}
