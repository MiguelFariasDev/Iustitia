using System.Collections.Concurrent;
using Advocacia.Modules.Drafting.Application.Abstractions;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>Cache em memória de embeddings, chaveado pelo HASH do conteúdo. Descarta o mais antigo ao encher.</summary>
public sealed class EmbeddingCache(int capacidade = 2000)
{
    private readonly ConcurrentDictionary<string, float[]> _items = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _order = new();

    public int Count => _items.Count;

    public bool TryGet(string hash, out float[] vector) => _items.TryGetValue(hash, out vector!);

    public void Set(string hash, float[] vector)
    {
        if (_items.TryAdd(hash, vector))
        {
            _order.Enqueue(hash);
        }

        while (_items.Count > capacidade && _order.TryDequeue(out var oldest))
        {
            _items.TryRemove(oldest, out _);
        }
    }
}

/// <summary>
/// Evita pagar GPU duas vezes pelo mesmo texto: consulta repetida (regerar a mesma peça) e trecho
/// idêntico entre documentos saem do cache; só o que falta vai ao Ollama, em UMA chamada.
/// </summary>
public sealed class CachedEmbeddingService(IEmbeddingService inner, EmbeddingCache cache) : IEmbeddingService
{
    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
    {
        var result = new float[inputs.Count][];
        var missing = new List<int>();

        for (var i = 0; i < inputs.Count; i++)
        {
            if (cache.TryGet(ContextSelector.HashText(inputs[i]), out var vector))
            {
                result[i] = vector;
            }
            else
            {
                missing.Add(i);
            }
        }

        if (missing.Count > 0)
        {
            var fresh = await inner.EmbedAsync(missing.Select(i => inputs[i]).ToList(), cancellationToken);
            if (fresh.Count != missing.Count)
            {
                throw new LlmServiceException("O serviço de embeddings devolveu um número inesperado de vetores.");
            }

            for (var k = 0; k < missing.Count; k++)
            {
                result[missing[k]] = fresh[k];
                cache.Set(ContextSelector.HashText(inputs[missing[k]]), fresh[k]);
            }
        }

        return result;
    }
}
