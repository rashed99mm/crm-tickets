using AutoMapper;
using AutoMapper.QueryableExtensions;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities;
using CustomerSupport.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CustomerSupport.Infrastructure.Persistence;

public class BaseRepository<T>(AppDbContext context, IConfigurationProvider config) : IRepository<T> where T : BaseEntity
{
    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Set<T>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);

    public virtual async Task<T?> GetTrackedAsync(Guid id, CancellationToken ct = default)
        => await context.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct);

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await context.Set<T>().AsNoTracking().FirstOrDefaultAsync(predicate, ct);

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await context.Set<T>().AnyAsync(predicate, ct);

    public virtual async Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default)
        => await context.Set<T>().AsNoTracking().ToListAsync(ct);

    public virtual async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate, CancellationToken ct = default)
    {
        IQueryable<T> query = context.Set<T>().AsNoTracking();
        if (predicate != null) query = query.Where(predicate);
        return await query.ToListAsync(ct);
    }

    public virtual async Task<IReadOnlyList<T>> ListOrderedAsync<TOrderKey>(
        Expression<Func<T, bool>>? predicate,
        Expression<Func<T, TOrderKey>> orderBy,
        bool descending,
        CancellationToken ct = default)
    {
        IQueryable<T> query = context.Set<T>().AsNoTracking();
        if (predicate != null) query = query.Where(predicate);
        query = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
        return await query.ToListAsync(ct);
    }

    public virtual async Task<IReadOnlyList<TDto>> ListProjectedAsync<TDto>(
        Expression<Func<T, bool>>? predicate,
        Expression<Func<T, TDto>> selectExpression,
        CancellationToken ct = default)
    {
        IQueryable<T> query = context.Set<T>().AsNoTracking();
        if (predicate != null) query = query.Where(predicate);
        return await query.Select(selectExpression).ToListAsync(ct);
    }

    public virtual async Task<IReadOnlyList<TDto>> ListProjectedOrderedAsync<TDto, TOrderKey>(
        Expression<Func<T, bool>>? predicate,
        Expression<Func<T, TDto>> selectExpression,
        Expression<Func<T, TOrderKey>> orderBy,
        bool descending,
        CancellationToken ct = default)
    {
        IQueryable<T> query = context.Set<T>().AsNoTracking();
        if (predicate != null) query = query.Where(predicate);
        query = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
        return await query.Select(selectExpression).ToListAsync(ct);
    }

    public virtual async Task<PaginatedList<TDto>> GetPagedAsync<TDto>(
        BasePagedQuery pagedQuery,
        Expression<Func<T, bool>>? filter,
        CancellationToken ct = default)
    {
        if (pagedQuery == null) throw new ArgumentNullException(nameof(pagedQuery));

        var query = context.Set<T>().AsQueryable();
        query = query.AsNoTracking();
        if (filter != null) query = query.Where(filter);

        var total = await query.CountAsync(ct);

        var pageIndex = Math.Max(pagedQuery.PageIndex, 1);
        var pageSize = Math.Max(pagedQuery.PageSize, 1);
        var skip = (pageIndex - 1) * pageSize;

        var sortBy = string.IsNullOrWhiteSpace(pagedQuery.SortBy) ? null : pagedQuery.SortBy;
        var sortDir = string.IsNullOrWhiteSpace(pagedQuery.SortDirection) ? "asc" : pagedQuery.SortDirection.ToLowerInvariant();

        if (!string.IsNullOrEmpty(sortBy))
        {
            try
            {
                query = query.ApplyOrdering(sortBy, sortDir == "desc");
            }
            catch
            {
                // Fallback: ignore invalid sort
            }
        }

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ProjectTo<TDto>(config)
            .ToListAsync(ct);

        return PaginatedList<TDto>.Create(items, total, pageIndex, pageSize);
    }

    public virtual async Task<PaginatedList<TDto>> GetPagedAsync<TDto>(
        BasePagedQuery pagedQuery,
        Expression<Func<T, bool>>? filter,
        Expression<Func<T, TDto>> selectExpression,
        CancellationToken ct = default)
    {
        if (pagedQuery == null) throw new ArgumentNullException(nameof(pagedQuery));
        if (selectExpression == null) throw new ArgumentNullException(nameof(selectExpression));

        var query = context.Set<T>().AsQueryable().AsNoTracking();

        if (filter != null)
            query = query.Where(filter);

        var total = await query.CountAsync(ct);

        var pageIndex = Math.Max(pagedQuery.PageIndex, 1);
        var pageSize = Math.Max(pagedQuery.PageSize, 1);
        var skip = (pageIndex - 1) * pageSize;

        var sortBy = string.IsNullOrWhiteSpace(pagedQuery.SortBy) ? null : pagedQuery.SortBy;
        var sortDir = string.IsNullOrWhiteSpace(pagedQuery.SortDirection) ? "asc" : pagedQuery.SortDirection.ToLowerInvariant();

        if (!string.IsNullOrEmpty(sortBy))
        {
            try
            {
                query = query.ApplyOrdering(sortBy, sortDir == "desc");
            }
            catch
            {
                // Fallback: ignore invalid sort
            }
        }

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .Select(selectExpression)
            .ToListAsync(ct);

        return PaginatedList<TDto>.Create(items, total, pageIndex, pageSize);
    }

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await context.Set<T>().AddAsync(entity, ct);

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
        => await context.Set<T>().AddRangeAsync(entities, ct);

    public virtual void SetOriginalValue(T entity, string propertyName, object? value)
    {
        context.Entry(entity).Property(propertyName).OriginalValue = value;
    }

    public virtual void Update(T entity)
        => context.Set<T>().Update(entity);

    public virtual void Remove(T entity)
        => context.Set<T>().Remove(entity);

    public virtual void RemoveRange(IEnumerable<T> entities)
        => context.Set<T>().RemoveRange(entities);

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        => predicate == null ? await context.Set<T>().CountAsync(ct) : await context.Set<T>().CountAsync(predicate, ct);
}
