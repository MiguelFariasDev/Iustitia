namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>Dimensão dos vetores do BGE-M3. Mudar de modelo de embedding exige reindexar tudo.</summary>
public static class EmbeddingConstants
{
    public const int Dimensoes = 1024;

    /// <summary>Textos por chamada ao servidor de embeddings.</summary>
    public const int Lote = 16;
}
