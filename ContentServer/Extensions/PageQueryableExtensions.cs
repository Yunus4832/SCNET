using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Extensions;

public interface IPagedQuery<TResponse> : IQuery<PagedData<TResponse>>
{
    int PageIndex { get; }

    int PageSize { get; }

    bool CountTotal { get; }
}

public static class PageQueryableExtensions
{
    public static async Task<PagedData<T>> ToPagedDataAsync<T>(
        this IQueryable<T> query,
        int pageIndex = 1,
        int pageSize = 10,
        bool countTotal = false,
        CancellationToken cancellationToken = default)
    {
        if (pageIndex <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "页码必须大于 0");
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "每页条数必须大于 0");
        }

        int num;
        if (countTotal)
        {
            num = await query.CountAsync(cancellationToken);
        }
        else
        {
            num = 0;
        }

        var totalCount = num;
        return new PagedData<T>(
            await query.Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken),
            totalCount,
            pageIndex,
            pageSize
        );
    }

    public static Task<PagedData<T>> ToPagedDataAsync<T>(
        this IQueryable<T> query,
        IPagedQuery<T> pagedQuery,
        CancellationToken cancellationToken = default)
    {
        return query.ToPagedDataAsync(
            pagedQuery.PageIndex,
            pagedQuery.PageSize, pagedQuery.CountTotal,
            cancellationToken);
    }

    public static Task<PagedData<T>> ToPagedDataAsync<T>(
        this IQueryable<T> query,
        IPageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        return query.ToPagedDataAsync(
            pageRequest.PageIndex,
            pageRequest.PageSize,
            pageRequest.CountTotal,
            cancellationToken
        );
    }
}
