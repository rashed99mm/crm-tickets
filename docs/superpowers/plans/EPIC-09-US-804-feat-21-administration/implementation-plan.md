# FEAT-21 — Administration (audit log + platform settings) Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make the already-inherited `AuditLog`/`IAuditService` actually populate (a real dead-code
bug fix), expose it via a query+admin screen, and ship a platform-settings admin screen against the
pre-existing settings backend (`US-801`–`US-803`).

**Architecture:** `AuditBehavior<TRequest, TResponse>` is a MediatR pipeline behavior that already
existed and was already registered's *sibling* concept (`IAuditService`) — but the behavior itself
was neither registered in the pipeline nor calling the service. This feature is mostly a wiring fix,
not new infrastructure.

**Tech Stack:** .NET 10, EF Core, MediatR, Angular 20.

**Spec:** [`docs/superpowers/specs/EPIC-09-US-804-administration.md`](../../specs/EPIC-09-US-804-administration.md)

## Global Constraints

- This feature's only failure path is validation (`400`) — no new `SystemCode`/`SystemCodeMap`
  entries needed (unlike `FEAT-16`, which did need them; the difference is recorded so a future
  reader doesn't assume every feature needs this step).

---

### Task 1: Fix `AuditBehavior` — the load-bearing bug (`US-801`)

**Files:**
- Modify: `backend/src/CustomerSupport.Application/Behaviors/AuditBehavior.cs`
- Modify: `backend/src/CustomerSupport.Application/ServiceCollectionExtensions.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/AuditLogEndpointTests.cs`

**Interfaces:**
- Consumes: `IAuditService.LogAsync(AuditLog, CancellationToken)` — already existed, fully wired for
  *writing*, but nothing ever called it.

**The actual bug, found while scoping this task, not invented for it**: `AuditBehavior` existed and
compiled, but (a) was never added to the MediatR pipeline registration — only `LoggingBehavior` and
`ResponseValidationBehavior` were — and (b) even if it had been registered, its `Handle` method only
called `ILogger.LogDebug`, never `IAuditService`. `AuditLogs` had been permanently empty since the
platform was adopted.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "145")]
public async Task AC145_CreatingAUser_WritesAnAuditLogRow()
{
    await _admin.PostAsJsonAsync("/api/Users", new { email = $"{Guid.NewGuid():N}@x.com", /* ... */ });

    var log = await _admin.GetFromJsonAsync<Response<PagedData<AuditLogRow>>>("/api/admin/audit-log?pageSize=50");
    log!.Data!.Items.Should().Contain(e => e.Action == "Created" && e.EntityType == "User");
}
```

Run: `dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AuditLogEndpointTests"`
Expected: FAIL — `AuditLogs` stays empty; the behavior never runs.

- [ ] **Step 2: Register the behavior — deliberately last**

```csharp
// Application/ServiceCollectionExtensions.cs
cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ResponseValidationBehavior<,>));
cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>)); // last — see below
```

Registered last so a validation failure's short-circuit means `AuditBehavior` never runs for a
request that never reached the handler — logging a failed, never-executed command would be a false
audit trail entry.

- [ ] **Step 3: Make the behavior actually call `IAuditService`**

```csharp
// backend/src/CustomerSupport.Application/Behaviors/AuditBehavior.cs
public class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly HashSet<string> AuditableCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreateUserCommand", "UpdateUserCommand", "DeleteUserCommand",
        "CreateContentCommand", "UpdateContentCommand", "DeleteContentCommand",
        "CreateNotificationCommand", "DeleteNotificationCommand",
        "CreatePlatformSettingCommand", "UpdatePlatformSettingCommand", "DeletePlatformSettingCommand"
    };

    private static readonly Dictionary<string, string> EntityTypeMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "CreateUserCommand", "User" }, { "UpdateUserCommand", "User" }, { "DeleteUserCommand", "User" },
        { "CreateContentCommand", "Content" }, { "UpdateContentCommand", "Content" }, { "DeleteContentCommand", "Content" },
        { "CreateNotificationCommand", "Notification" }, { "DeleteNotificationCommand", "Notification" },
        { "CreatePlatformSettingCommand", "PlatformSetting" }, { "UpdatePlatformSettingCommand", "PlatformSetting" },
        { "DeletePlatformSettingCommand", "PlatformSetting" }
    };

    private readonly IUserContext _userContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

    public AuditBehavior(IUserContext userContext, IAuditService auditService, ILogger<AuditBehavior<TRequest, TResponse>> logger)
    {
        _userContext = userContext; _auditService = auditService; _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        if (!AuditableCommands.Contains(requestName)) return await next();

        var userId = _userContext.UserId;
        var response = await next();
        await RecordAsync(requestName, request, response, userId, ct);
        return response;
    }

    /// <summary>Best-effort, generic across every auditable command — no "before" snapshot exists,
    /// so OldValues is always null. Success/Data/Id are read via reflection since this behavior is
    /// generic over TResponse and TRequest with no common interface to check against.</summary>
    private async Task RecordAsync(string requestName, TRequest request, TResponse response, Guid userId, CancellationToken ct)
    {
        if (typeof(TResponse).GetProperty("Success")?.GetValue(response) is not true) return;

        var entityId = ResolveEntityId(request, response);
        if (entityId is null) return;

        var entityType = EntityTypeMapping.GetValueOrDefault(requestName, "Unknown");
        var action = requestName.StartsWith("Create", StringComparison.OrdinalIgnoreCase) ? "Created"
            : requestName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ? "Deleted" : "Updated";

        var auditLog = AuditLog.Create(userId, _userContext.Email, action, entityType, entityId.Value,
            oldValues: null, newValues: action == "Deleted" ? null : request);

        await _auditService.LogAsync(auditLog, ct);
    }

    private static Guid? ResolveEntityId(TRequest request, TResponse response)
    {
        if (typeof(TResponse).GetProperty("Data")?.GetValue(response) is Guid fromResponse) return fromResponse;
        if (typeof(TRequest).GetProperty("Id")?.GetValue(request) is Guid fromRequest) return fromRequest;
        return null;
    }
}
```

No `OldValues` diff (spec A2) — the behavior has no "before" read to diff against.

- [ ] **Step 4: Run test to verify it passes, commit**

Run: `dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AuditLogEndpointTests"`
Expected: PASS.

```bash
git add backend/src/CustomerSupport.Application/Behaviors/AuditBehavior.cs backend/src/CustomerSupport.Application/ServiceCollectionExtensions.cs backend/tests/CustomerSupport.Tests/Integration/AuditLogEndpointTests.cs
git commit -m "fix(audit): register AuditBehavior and make it actually call IAuditService (AC-145)"
```

---

### Task 2: `GET /api/admin/audit-log` (`US-801`, `AC-140`)

**Files:**
- Create: `Features/Admin/Queries/GetAuditLog/`
- Create: `backend/src/CustomerSupport.InternalApi/Controllers/AdminController.cs`

**Interfaces:**
- Consumes: `IRepository<AuditLog>.GetPagedAsync` — **explicit `SortBy`/`SortDirection` override
  required**, since the repository has no default ordering at all when `SortBy` is unset; without
  it "newest first" (`AC-140`) would be accidental, not guaranteed.

- [ ] **Step 1–3: query + handler + controller**

```csharp
// backend/src/CustomerSupport.Application/Features/Admin/Queries/GetAuditLog/GetAuditLogQueryHandler.cs
public class GetAuditLogQueryHandler(IRepository<AuditLog> auditLogs, IMessageFactory messages)
    : IQueryHandler<GetAuditLogQuery, Response<PaginatedList<AuditLogDto>>>
{
    public async Task<Response<PaginatedList<AuditLogDto>>> Handle(GetAuditLogQuery request, CancellationToken ct)
    {
        var filter = PredicateBuilder.True<AuditLog>()
            .WhereIf(!string.IsNullOrWhiteSpace(request.ActionType), a => a.Action == request.ActionType!)
            .WhereIf(request.UserId.HasValue, a => a.UserId == request.UserId!.Value);

        var page = await auditLogs.GetPagedAsync(request, filter,
            a => new AuditLogDto(a.Id, a.UserId, a.UserName, a.Action, a.EntityType, a.EntityId,
                a.OldValues, a.NewValues, a.IpAddress, a.UserAgent, a.CreatedAt), ct);

        return messages.Success(page, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
```

```csharp
// GetAuditLogQuery.cs — constructor sets SortBy/SortDirection explicitly (AC-140)
public class GetAuditLogQuery : BasePagedQuery, IQuery<Response<PaginatedList<AuditLogDto>>>
{
    public GetAuditLogQuery() { SortBy = nameof(AuditLog.CreatedAt); SortDirection = "desc"; }
    public string? ActionType { get; init; }
    public Guid? UserId { get; init; }
}
```

```csharp
// backend/src/CustomerSupport.InternalApi/Controllers/AdminController.cs
[ApiController]
[Route("api/admin")]
[ApiVersion("1.0")]
[Authorize(Policy = "UserManagement")]
public class AdminController(IMediator mediator) : ControllerBase
{
    [HttpGet("audit-log")]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? actionType = null, [FromQuery] Guid? userId = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetAuditLogQuery { PageIndex = page, PageSize = pageSize, ActionType = actionType, UserId = userId }, ct);
        return this.ToActionResult(result);
    }
}
```

- [ ] **Step 4: Run, commit**

```bash
git commit -m "feat(admin): GET /api/admin/audit-log, newest-first (AC-140)"
```

---

### Task 3: `AuditLogComponent` (`US-802`, frontend)

**Files:**
- Create: `frontend/projects/common/src/lib/admin/audit-log.api.ts`
- Create: `frontend/projects/admin-app/src/app/features/admin/audit-log.component.{ts,html}`

Filterable (`actionType`, `userId`), paginated table, `AsyncState`-driven, row-click detail panel.
Same list-screen shape as `DepartmentsComponent`, extended with two filter inputs.

**A hardcoded-string sweep failure, fixed during implementation**: the template used a raw `·`
character between two interpolated values in the detail panel; `no-hardcoded-strings.spec.ts`
correctly flagged it (its allowlist is deliberately narrow). Fixed by using `—`, already on the
allowlist, rather than growing it.

```bash
git commit -m "feat(admin): AuditLogComponent (US-802)"
```

---

### Task 4: `PlatformSettingsComponent` (`US-803`, frontend)

**Files:**
- Create: `frontend/projects/admin-app/src/app/features/admin/platform-settings.component.{ts,html}`

**No backend change needed** — `PlatformSettingsController` already existed from the inherited
platform baseline. List + inline per-row edit, consuming it directly.

```bash
git commit -m "feat(admin): PlatformSettingsComponent (US-803)"
```

### Task 5: Gap-audit cleanup (`US-803`, `US-802`, `US-314`)

The 2026-08-29 UI gap report reopened this area for visible dead controls:

- Branding form controls must bind `logoUrl`, `primaryColor`, and `accentColor` to matching inputs.
- A logo upload control must be a real file input with preview.
- Saving branding must update runtime CSS variables through `BrandingStore`.
- Generic platform settings must render editable rows, using the existing `PlatformSettingApi`.
- Audit log export must download the current visible rows as CSV.

Implementation and evidence are tracked in
[`../EPIC-13-US-311-ui-gap-closure/implementation-plan.md`](../EPIC-13-US-311-ui-gap-closure/implementation-plan.md).

## Definition of done

`AC-140`, `AC-145` each covered by a test naming it. Evidence already recorded in this folder's
`README.md`: 6/6 filtered `AuditLogEndpointTests`, 351/351 full suite, both frontend projects
building and testing clean at the time this feature shipped.

## Not shipped (spec A4, recorded not silently dropped)

- `US-804`/`US-805` (permission entity + admin UI) — a genuinely separate, larger authorization
  capability.
- Date-range filtering on the audit query (spec A5) — the backend query only supports
  `actionType`/`userId`.
