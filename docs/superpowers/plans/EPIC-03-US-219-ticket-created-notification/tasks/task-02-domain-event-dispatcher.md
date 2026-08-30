# Task 02 — Domain-event dispatcher and the SaveChanges hook

**Criteria:** `AC-N4`, `AC-N6`

## Files

- `Application/Events/IDomainEventHandler.cs` — new handler marker.
- `Application/Events/IDomainEventDispatcher.cs` — new dispatch contract.
- `Application/Events/DomainEventDispatcher.cs` — runtime resolving handlers from `IServiceProvider`.
- `Application/ServiceCollectionExtensions.cs` — registrar for `IDomainEventHandler<>`.
- `Infrastructure/Persistence/AppDbContext.cs:54` — dispatch hook in `SaveChangesAsync`.
- `Infrastructure/ServiceCollectionExtensions.cs` — register the dispatcher + handlers.

## Steps (TDD — failing test first)

1. Write failing unit test(s) for `DomainEventDispatcher`:
   - a handler registered for `TEvent` receives the raised event (`AC-N4` dispatch happens);
   - an event with no registered handler dispatches no-op and does not throw;
   - a handler that throws does not propagate to the caller (`AC-N4` swallow-and-log);
   - de-dup: a dispatcher handed a collection dispatches each distinct event once.
2. Add `IDomainEventHandler<TEvent>`:

   ```csharp
   public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
   { Task Handle(TEvent domainEvent, CancellationToken ct = default); }
   ```

2. Add `IDomainEventDispatcher`:

   ```csharp
   public interface IDomainEventDispatcher
   { Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default); }
   ```

3. Implement `DomainEventDispatcher` using `IServiceProvider` (same DI abstraction the pipeline
   behaviors already use). For each event type `T`, `provider.GetServices(typeof(IDomainEventHandler<T>))`
   and invoke each `Handle`. If a handler throws, log via `ILogger<DomainEventDispatcher>` and continue —
   never fail the already-committed save (`AC-N4`).
4. Register handlers generically in Application: reflection over the Application assembly registering
   every `IDomainEventHandler<TEvent>` as scoped against its closed generic (or explicit registration
   of `TicketCreatedEventHandler`). Pin whichever is simplest; the plan prefers the explicit
   registration to keep the type cheap to inspect.
5. Hook `SaveChangesAsync`:

   ```csharp
   public async Task<int> SaveChangesAsync(CancellationToken ct = default)
   {
       var result = await _context.SaveChangesAsync(ct);  // the committed save
       await PublishDomainEventsAsync(ct);                // AFTER commit, in a new scope
       return result;
   }
   ```

   `PublishDomainEventsAsync` (in `UnitOfWork`, not `AppDbContext`) collects only *tracked*
   added/modified `BaseEntity` `DomainEvents`, clears them on each entity (guaranteeing at-most-once,
   `AC-N4`), and — only if any were raised (`AC-N6`: no events → no scope, no dispatch) — creates a
   scope from the **injected** `IServiceScopeFactory`, resolves the scoped `IDomainEventDispatcher`,
   and dispatches. Handlers in that scope use their own `AppDbContext`, so a handler's `SaveChanges`
   cannot re-enter the completing change tracker.

   > **Deviation (recorded):** the plan originally put this hook in `AppDbContext.SaveChangesAsync`
   > and resolved the scope factory via `this.GetInfrastructure().GetService<IServiceScopeFactory>()`.
   > An experiment against a real `WebApplicationFactory` host proved that `GetInfrastructure()`
   > returns the EF **internal** provider, from which no application service (`IDomainEventDispatcher`,
   > `UserManager<ApplicationUser>`, `IMediator`) is resolvable — it returned `null` for all of them.
   > So the hook was moved to `UnitOfWork`, a scoped application service that injects the real
   > `IServiceScopeFactory` (always registered). This satisfies `AC-N4`/`AC-N6` identically, keeps
   > `AppDbContext`'s constructor and `DesignTimeDbContextFactory` untouched, and preserves the
   > "dispatch after commit, in a fresh scope" isolation the spec requires.

6. Register `IDomainEventDispatcher` in
   `Application/ServiceCollectionExtensions.RegisterPlatformApplication`, and auto-register every
   `IDomainEventHandler<TEvent>` in the Application assembly against its closed generic (small
   reflection scan; keeps future handlers one-class each).

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~DomainEventDispatcher|FullyQualifiedName~UnitOfWorkPublishTests"`

**Commit:** `feat: add domain event dispatcher to the save pipeline`

**Deviation log:** See the note in step 5 — dispatch lives in `UnitOfWork`, not `AppDbContext`, because the EF internal provider cannot resolve application services (verified against the real host). All other steps as written; dispatcher registered in Application (not Infrastructure) since it consumes `IServiceProvider` like the other Application behaviors.
