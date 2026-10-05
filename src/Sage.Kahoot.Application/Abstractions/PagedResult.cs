namespace Sage.Kahoot.Application.Abstractions;

/// <summary>Página de resultados com o total de registros, para as listagens paginadas.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, long TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static int NormalizePage(int page) => page < 1 ? 1 : page;

    public static int NormalizePageSize(int pageSize) =>
        pageSize is < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
}
