using CustomerSupport.Application.Events;
using CustomerSupport.Domain.Entities;
using CustomerSupport.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerSupport.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;
    private IDbContextTransaction? _currentTx;

    public UnitOfWork(AppDbContext context, IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _scopeFactory = scopeFactory;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Domain events must be published only once the write has committed (AC-N4). Handlers run on
        // an independent scope/AppDbContext, so a handler's own SaveChanges can never re-enter this
        // completing change tracker and their failures are swallowed by the dispatcher — never the
        // save that just succeeded.
        var result = await _context.SaveChangesAsync(ct);
        await PublishDomainEventsAsync(ct);
        return result;
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTx != null) return;
        _currentTx = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTx == null) return;
        await _context.SaveChangesAsync(ct);
        await _currentTx.CommitAsync(ct);
        await _currentTx.DisposeAsync();
        _currentTx = null;
        // Publish after the transaction commits, so published events are never rolled back.
        await PublishDomainEventsAsync(ct);
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTx == null) return;
        try
        {
            await _currentTx.RollbackAsync(ct);
        }
        finally
        {
            await _currentTx.DisposeAsync();
            _currentTx = null;
        }
    }

    /// <summary>
    /// Collects and clears the domain events raised on tracked aggregates, then dispatches them on a
    /// fresh scope so handlers are isolated from the completing change tracker (AC-N4). Empty event
    /// set → no scope is opened and nothing is dispatched (AC-N6).
    /// </summary>
    private async Task PublishDomainEventsAsync(CancellationToken ct)
    {
        var events = _context.ChangeTracker.Entries<BaseEntity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        if (events.Count == 0)
        {
            return;
        }

        // Clearing before dispatch makes a second save in the same unit of work unable to re-publish
        // the same events (AC-N4: at most once).
        foreach (var entry in _context.ChangeTracker.Entries<BaseEntity>())
        {
            entry.Entity.ClearDomainEvents();
        }

        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        await dispatcher.DispatchAsync(events, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentTx != null)
        {
            await _currentTx.DisposeAsync();
            _currentTx = null;
        }
    }
}
