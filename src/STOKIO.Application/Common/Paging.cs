namespace STOKIO.Application.Common;

public static class Paging
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var normalizedPage = page.GetValueOrDefault(DefaultPage);
        if (normalizedPage < 1)
        {
            normalizedPage = DefaultPage;
        }

        var normalizedPageSize = pageSize.GetValueOrDefault(DefaultPageSize);
        if (normalizedPageSize < 1)
        {
            normalizedPageSize = DefaultPageSize;
        }

        if (normalizedPageSize > MaxPageSize)
        {
            normalizedPageSize = MaxPageSize;
        }

        return (normalizedPage, normalizedPageSize);
    }
}

