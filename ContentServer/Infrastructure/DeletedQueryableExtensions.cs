using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Domain;

namespace ContentServer.Infrastructure;

public static class DeletedQueryableExtensions
{
    public static IQueryable<TEntity> IncludeDeleted<TEntity>(this IQueryable<TEntity> query,
        bool includeDeleted = true) where TEntity : class =>
        includeDeleted ? query.IgnoreQueryFilters() : query;

    public static IQueryable<TEntity> WhereDeleted<TEntity>(this IQueryable<TEntity> query,
        Deleted deleted) where TEntity : class =>
        query.IgnoreQueryFilters().Where(entity => EF.Property<Deleted>(entity, "Deleted") == deleted);
}
