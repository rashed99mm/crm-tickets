using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities;
using System.Linq.Expressions;

namespace CustomerSupport.Domain.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<T?> GetTrackedAsync(Guid id, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListOrderedAsync<TOrderKey>(
        Expression<Func<T, bool>>? predicate,
        Expression<Func<T, TOrderKey>> orderBy,
        bool descending,
        CancellationToken ct = default);
    Task<IReadOnlyList<TDto>> ListProjectedAsync<TDto>(
        Expression<Func<T, bool>>? predicate,
        Expression<Func<T, TDto>> selectExpression,
        CancellationToken ct = default);
    Task<IReadOnlyList<TDto>> ListProjectedOrderedAsync<TDto, TOrderKey>(
        Expression<Func<T, bool>>? predicate,
        Expression<Func<T, TDto>> selectExpression,
        Expression<Func<T, TOrderKey>> orderBy,
        bool descending,
        CancellationToken ct = default);
    Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, Expression<Func<T, bool>>? filter, CancellationToken ct = default);
    Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, Expression<Func<T, bool>>? filter, Expression<Func<T, TDto>> selectExpression, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    void Update(T entity);

    /// <summary>
    /// Overrides what the change tracker believes the row looked like when it was loaded.
    ///
    /// Exists for optimistic concurrency (AC-41): the caller echoes back the version it read, and
    /// this makes EF compare the stored row against <em>that</em> rather than against the value
    /// this request happened to load a moment ago. Without it a concurrency token cannot detect a
    /// conflict across two separate requests, because each one loads the current value.
    /// </summary>
    void SetOriginalValue(T entity, string propertyName, object? value);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
}
