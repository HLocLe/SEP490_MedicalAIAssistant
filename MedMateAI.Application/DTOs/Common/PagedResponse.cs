using MedMateAI.Domain.Common;

namespace MedMateAI.Application.DTOs.Common;

public sealed class PagedResponse<TItem>
{
    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public IReadOnlyList<TItem> Items { get; set; } = Array.Empty<TItem>();

    public static PagedResponse<TItem> From<TSource>(
        PagedResult<TSource> paged,
        Func<TSource, TItem> mapItem)
    {
        return new PagedResponse<TItem>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items.Select(mapItem).ToList(),
        };
    }
}
