namespace Advocacia.Modules.Drafting.Infrastructure.Ollama;

/// <summary>Seção <c>Ollama</c>. A URL real vem de <c>OLLAMA__BASEURL</c> (nunca do appsettings versionado).</summary>
public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";

    public string Modelo { get; set; } = "qwen3:8b";

    public string ModeloEmbedding { get; set; } = "bge-m3";

    /// <summary>Tempo máximo de UMA geração inteira (do envio ao último fragmento).</summary>
    public int TimeoutSegundos { get; set; } = 600;

    /// <summary>Token de um proxy reverso à frente do Ollama (o Ollama puro não tem autenticação).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Baixa = mais fiel ao texto fornecido. Peça jurídica não é lugar de criatividade.</summary>
    public double Temperatura { get; set; } = 0.2;

    public double TopP { get; set; } = 0.9;

    /// <summary>
    /// Janela de contexto pedida ao Ollama (prompt + resposta). O padrão dele é 4096 e TRUNCA EM
    /// SILÊNCIO o começo do prompt (o system prompt!) quando estoura. Orçamento: ~450 (system) +
    /// até ~3.500 (fatos/pedidos/instruções) + 8.000 (contexto do RAG) + 4.096 (resposta) ≈ 16.000.
    /// Com 8192 e contexto de 8.000 tokens a conta NÃO fecha — reduza <c>Rag:MaxTokensContexto</c>
    /// (para ~2.500) se precisar de uma janela menor.
    /// </summary>
    public int NumCtx { get; set; } = 16384;

    /// <summary>Teto de tokens da minuta (num_predict).</summary>
    public int NumPredict { get; set; } = 4096;

    /// <summary>Quanto o modelo fica carregado na GPU depois do último uso (evita recarregar a cada peça).</summary>
    public string KeepAlive { get; set; } = "10m";

    /// <summary>
    /// Permite http:// para um host que não seja loopback. Desligado por padrão: fatos de processo
    /// trafegando em texto claro pela internet pública (caso típico de uma porta aberta na Vast.ai)
    /// violam o sigilo profissional. Use TLS (proxy reverso) ou túnel; ligar isto é decisão consciente.
    /// </summary>
    public bool AllowInsecureHttp { get; set; }

    public string? Validate() => TransportGuard.Validate("Ollama", BaseUrl, ApiKey, AllowInsecureHttp);
}

/// <summary>Regra de transporte compartilhada por Ollama e Qdrant.</summary>
public static class TransportGuard
{
    public static string? Validate(string name, string baseUrl, string? apiKey, bool allowInsecureHttp)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return $"{name}:BaseUrl inválida (esperado http:// ou https://).";
        }

        var secure = uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback;
        if (!secure && !allowInsecureHttp)
        {
            return $"{name}:BaseUrl usa http:// para um host que não é local. Use https:// (proxy reverso com TLS) ou túnel; " +
                   $"só ligue {name}:AllowInsecureHttp se tiver aceito esse risco por escrito (ADR-072).";
        }

        if (!secure && !string.IsNullOrWhiteSpace(apiKey))
        {
            return $"{name}:ApiKey configurada mas o transporte não é seguro: a chave trafegaria em texto claro.";
        }

        return null;
    }
}
