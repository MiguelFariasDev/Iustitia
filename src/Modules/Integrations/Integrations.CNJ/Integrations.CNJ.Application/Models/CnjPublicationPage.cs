namespace Advocacia.Modules.Integrations.CNJ.Application.Models;

/// <summary>Uma página de resultados do DJEN/CNJ.</summary>
/// <param name="Items">Publicações da página.</param>
/// <param name="Page">Número da página devolvida (1-based).</param>
/// <param name="PageSize">Tamanho de página solicitado.</param>
/// <param name="TotalCount">Total de publicações que satisfazem o critério, somando todas as páginas.</param>
public sealed record CnjPublicationPage(
    IReadOnlyList<CnjPublicationDto> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public static CnjPublicationPage Empty(int page, int pageSize) => new([], page, pageSize, 0);

    /// <summary>
    /// Se existe pelo menos uma página seguinte.
    ///
    /// Uma página CHEIA sempre manda continuar, independentemente do que o
    /// <see cref="TotalCount"/> diga — e essa é a regra que importa, não uma otimização.
    /// Medido contra a API real na Etapa 1.3 (ver docs/poc/cnj-poc-report.md): o DJEN devolve
    /// um <c>count</c> ERRADO quando <c>itensPorPagina=100</c>, repetindo o tamanho da página
    /// em vez do total. A mesma consulta com <c>itensPorPagina=50</c> devolve 14.138; com 100,
    /// devolve 100.
    ///
    /// Confiar no count nesse caso fazia a captura parar na primeira página e perder ~14 mil
    /// publicações SEM erro, sem log e sem métrica — o tipo de falha que só aparece quando um
    /// advogado perde um prazo. Por isso a condição é um OU: o count só pode ADICIONAR
    /// páginas, nunca encerrar a varredura antes de uma página incompleta.
    /// </summary>
    public bool HasMorePages => Items.Count >= PageSize || Page * PageSize < TotalCount;

    /// <summary>
    /// Se o <see cref="TotalCount"/> declarado pela origem é suspeito de ser o tamanho da
    /// página em vez do total (ver <see cref="HasMorePages"/>). Usado só para log/diagnóstico
    /// — a paginação não depende disso para estar correta.
    /// </summary>
    public bool HasSuspiciousTotalCount => TotalCount == PageSize && Items.Count >= PageSize;
}
