namespace Advocacia.BuildingBlocks.Application.PagedList;

/// <summary>
/// Envelope padrão de paginação para toda resposta de lista da API — mantém o
/// contrato uniforme entre módulos (processos, tarefas, usuários, etc.).
/// </summary>
public sealed class PagedList<T>
{
    private PagedList(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    public static PagedList<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount) =>
        new(items, page, pageSize, totalCount);

    public static PagedList<T> Empty(int page, int pageSize) => new([], page, pageSize, 0);
}
