namespace CustomerSupport.Domain;

public class PaginatedList<T>
{
    public IReadOnlyList<T> Items { get; }
    public int PageIndex { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    private PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
    {
        Items = items.AsReadOnly();
        PageIndex = Math.Max(1, pageIndex);
        PageSize = Math.Max(1, pageSize);
        TotalCount = count;
    }

    public static PaginatedList<T> Create(IEnumerable<T> items, int count, int pageIndex, int pageSize)
    {
        var itemList = items.ToList();
        return new PaginatedList<T>(itemList, count, pageIndex, pageSize);
    }
}
