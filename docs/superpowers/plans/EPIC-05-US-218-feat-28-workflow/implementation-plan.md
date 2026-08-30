# FEAT-28 — The 8-state lifecycle and the domain enrichment it depends on · backend + shared frontend status model

**Date:** 2026-08-28
**Feature:** `FEAT-28`, sprint 17 · **vertical** (backend + hosted in idependence: the shared frontend status model ships inside this feature per `delivery-plan.md:382-386`) · 29 points
**Spec:** `AC-501`…`AC-512` (the workflow block `AC-501…AC-507` + the enrichment block `AC-508…AC-512`); the EPIC-14 delivery-table also cites `AC-536`, which is **undefined** — see D3
**Frontend counterpart:** `US-919` ships in this feature; `US-912`/`US-920` (self-assign control, escalation banner) ship with `FEAT-30`
**Depends on:** the shipped ticket aggregate and its transition table (`FEAT-06`), the SLA pause + escalation scanner (`FEAT-17`), the conversation record (`FEAT-14`), the org-structure CRUD (Departments/Branches, `FEAT-16`)

> **Execution note.** This plan is written to be executed without re-researching the codebase: every
> task names the real files it touches, embeds the code that replaces what is there, and names the
> tests it writes. Stories `US-901…US-907` and `US-919` carry their own, spec-free wording; this plan
> is the only place the machinery is spelled out.
>
> The plan is split into the two delivery slices the delivery plan mandates
> (`delivery-plan.md:382-386`): **slice 0 — domain enrichment** (Tasks 1–4), then **slice 1 — the
> 8-state machine** (Tasks 5–9). Each task names the `AC-n` its tests cite. All commands are
> PowerShell 5.1 (`;` then `if ($?)`, never `&&`).

---

## What already exists, and what that changes

The ticket machine is currently five states with a closed table
(`backend/src/CustomerSupport.Domain/ValueObjects/TicketStatus.cs:12-20,66-82`):

```
New → Open → Pending → Resolved → Closed
```

`TicketStatus.All` is `[New, Open, Pending, Resolved, Closed]`; `CanTransitionTo` allows 8 of the 25
pairs; `IsReopenTo` targets `Open`. `Ticket.ChangeStatus`
(`backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs:152-180`) applies a transition or
throws `InvalidOperationException`; the SLA pause keys on **the literal string `"Pending"`**
(`Ticket.cs:187-213`, `const string pending = "Pending";`).

The organisational columns already exist as dormant schema:

- `Ticket.DepartmentId`/`Ticket.BranchId` — nullable, "unset by every path today" (`Ticket.cs:26-32`).
- `ApplicationUser.DepartmentId`/`ApplicationUser.BranchId` — nullable, no setter (`ApplicationUser.cs:15-17`).
- There is **no `Team` entity**, no `TeamId` anywhere, and no lifecycle timestamps
  (`FirstResponseAt`/`LastResponseAt`/`ResolvedAt`/`ClosedAt`), no `EscalationAssigneeId`.

The error plumbing is the `ApplicationErrors` keys → `SystemCodeMap` → `SystemCode` → `Resources.yaml`
chain. The next free codes are `ERR077+`, `CON070+`, `VAL066+` (last used `ERR076`, `CON069`, `VAL065`).

**The one invariant that must not bend is the dependency rule.** Nothing below adds a
`ProjectReference` to `CustomerSupport.Domain` or makes `Application` touch `Infrastructure`; all
handlers keep working against `IRepository<T>` and the existing port interfaces.

Three plans decisions that override the spec text are recorded as deviations at the bottom (D1–D3):
the schema migration is **not** data-only (Status column must widen), `AC-505`'s wire answer is the
existing `409` and not a `403`, and `AC-536` does not exist.

---

## Slice 0 — domain enrichment

### Task 1: `Team` entity + CRUD + FK columns + migration + seeder (`US-905`; `AC-508`, `AC-509`)

**Files:**
- `backend/src/CustomerSupport.Domain/Entities/Organisation/Team.cs` — new
- `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TeamConfiguration.cs` — new
- `backend/src/CustomerSupport.Application/Features/Organisation/Dtos/TeamDtos.cs` — new
- `backend/src/CustomerSupport.Application/Features/Organisation/Commands/CreateTeam/{Command,Validator,Handler}.cs` — new
- `backend/src/CustomerSupport.Application/Features/Organisation/Commands/UpdateTeam/` — new
- `backend/src/CustomerSupport.Application/Features/Organisation/Commands/DeactivateTeam/` — new
- `backend/src/CustomerSupport.Application/Features/Organisation/Queries/GetTeams/` and `GetTeamById/` — new
- `backend/src/CustomerSupport.InternalApi/Controllers/TeamsController.cs` — new (mirror `DepartmentsController.cs`, `AC-120` split: `Authenticated` reads, `Admin` mutations)
- `backend/src/CustomerSupport.Domain/Entities/Identity/ApplicationUser.cs` — add `TeamId`
- `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs` — add `TeamId`
- `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ApplicationUserConfiguration.cs` — `Team` FK
- `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketConfiguration.cs` — `Team` FK
- `backend/src/CustomerSupport.Infrastructure/Seeders/TeamSeeder.cs` — new (one default team per department; well-known id; idempotent)
- `backend/src/CustomerSupport.Infrastructure/Seeders/IdentitySeeder.cs` — wire seeded staff into the default org
- `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs` + `Messages/SystemCodeMap.cs` + `Messages/SystemCode.cs` + `Api.Shared/Localization/Resources.yaml` — team key chain
- `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs` — `DbSet<Team>`
- `backend/tests/CustomerSupport.Tests/...` — new integration tests `Team*`

**Interfaces:**
- `IRepository<Team>` (generic), `IUnitOfWork`, `IDbExceptionTranslator`, `IMessageFactory` — all already registered for `Department`.

**Step 1 — the failing unit tests first.** New domain test file `Tests/Unit/Domain/TeamTests.cs`:

```csharp
// TC-01 (AC-508) — Team_Create_WithValidName
var team = Team.Create("Billing Support", DefaultDepartmentId, managerId: null, id: WellKnown);
team.Name.Should().Be("Billing Support");
team.DepartmentId.Should().Be(DefaultDepartmentId);
team.IsActive.Should().BeTrue();

// TC-02 (AC-508) — Team_Deactivate_TogglesIsActive
team.Deactivate();
team.IsActive.Should().BeFalse();
```

**Step 2 — the entity.**

```csharp
namespace CustomerSupport.Domain.Entities.Organisation;

/// <summary>
/// Groups agents under a department (US-905, AC-508). The same shape as <see cref="Department"/>,
/// plus the owning <see cref="DepartmentId"/>; the drill-down Org→Branch→Dept→Team→Agent needs
/// exactly this depth (spec A6: teams do not nest).
/// </summary>
public class Team : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public Guid DepartmentId { get; private set; }

    /// <summary>Unvalidated, exactly like <c>Department.ManagerId</c> (spec A5 of the org spec).</summary>
    public Guid? ManagerId { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Well-known <paramref name="id"/> only for the seeder, like <c>Department.Create</c>.</summary>
    public static Team Create(string name, Guid departmentId, Guid? managerId, Guid? id = null)
    {
        if (departmentId == Guid.Empty)
        {
            throw new ArgumentException("A department is required", nameof(departmentId));
        }

        return new Team
        {
            Id = id ?? Guid.NewGuid(),
            Name = ValidateName(name),
            DepartmentId = departmentId,
            ManagerId = managerId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, Guid? managerId)
    {
        Name = ValidateName(name);
        ManagerId = managerId;
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required", nameof(name));
        }

        if (name.Length > 200)
        {
            throw new ArgumentException("Name must not exceed 200 characters", nameof(name));
        }

        return name.Trim();
    }
}
```

**Step 3 — the configuration** (`TeamConfiguration.cs`), unique **within its department** (AC-508):

```csharp
builder.ToTable("Teams");
builder.HasKey(x => x.Id);
builder.Property(x => x.Name).HasMaxLength(200).IsRequired();

builder.HasOne<Department>()
    .WithMany()
    .HasForeignKey(x => x.DepartmentId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(x => new { x.DepartmentId, x.Name })
    .IsUnique()
    .HasFilter("[IsDeleted] = 0")
    .HasDatabaseName("UX_Teams_DepartmentName");
```

**Step 4 — the DTOs and CRUD** (`TeamDtos.cs`):

```csharp
public record TeamDto(Guid Id, string Name, Guid DepartmentId, Guid? ManagerId, bool IsActive, DateTime CreatedAt);
public record TeamRequest(string Name, Guid DepartmentId, Guid? ManagerId);
```

Copy the five `Department` command/query shapes (`CreateDepartmentCommand(+Validator+Handler)`,
`UpdateDepartmentCommand`, `DeactivateDepartmentCommand`, `GetDepartmentsQuery`,
`GetDepartmentByIdQuery`) onto `Team`, using `Team.Create`/`Update`/`Deactivate`, and the
`Department` handler pattern for the unique-violation refusal:

```csharp
catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
{
    return messages.Fail<Guid>(ApplicationErrors.Team.NAME_EXISTS, MessageType.Conflict);
}
```

The validator reuses `Validation.ORG_NAME_REQUIRED`/`ORG_NAME_MAX_LENGTH` and adds
`DEPARTMENT_ID_REQUIRED` (new `VAL066`, `SystemCodeMap` entry). `TeamsController` exactly mirrors
`DepartmentsController` (`AC-120` split: `[Authorize(Policy = "Authenticated")]` reads, `[Authorize(Policy = "Admin")]` writes).

**Step 5 — the FK columns** (`AC-509`).

`ApplicationUser.cs` — add after `BranchId`:

```csharp
public Guid? TeamId { get; private set; }
```

`Ticket.cs` — add after `BranchId`:

```csharp
public Guid? TeamId { get; private set; }
```

`ApplicationUserConfiguration.cs` — after the `Branch` `HasOne`:

```csharp
builder.HasOne<Team>()
    .WithMany()
    .HasForeignKey(x => x.TeamId)
    .OnDelete(DeleteBehavior.Restrict);
```

`TicketConfiguration.cs` — after the `Branch` `HasOne`:

```csharp
builder.HasOne<Team>()
    .WithMany()
    .HasForeignKey(x => x.TeamId)
    .OnDelete(DeleteBehavior.Restrict);
```

**Step 6 — the new error keys** (`AC-508` CRUD messages). Add to `ApplicationErrors`:

```csharp
public static class Team
{
    public const string NOT_FOUND = "TEAM_NOT_FOUND";
    public const string CREATED = "TEAM_CREATED";
    public const string UPDATED = "TEAM_UPDATED";
    public const string DEACTIVATED = "TEAM_DEACTIVATED";
    public const string NAME_EXISTS = "TEAM_NAME_EXISTS";
}
```

`SystemCode` get `ERR077 = "ERR077"` (not found), `ERR078 = "ERR078"` (name exists), `CON070`/`CON071`/`CON072`
(created/updated/deactivated), `VAL066` (department required). `SystemCodeMap` gets the five `TEAM_*`
entries plus `DEPARTMENT_ID_REQUIRED`. Add the five `En`/`Ar` lines to
`Resources.yaml` following the `Department` block template (lines ~675-734).

**Step 7 — the seeder.** `TeamSeeder.cs` mirrors `DepartmentBranchSeeder` (idempotent,
`IgnoreQueryFilters`, well-known id, insert-race tolerance):

```csharp
public static readonly Guid DefaultTeamId = new("00000000-0000-0000-0000-000000000002");

var hasTeam = await db.Teams.IgnoreQueryFilters().AnyAsync(t => t.Id == DefaultTeamId, ct);
if (!hasTeam)
{
    db.Teams.Add(Team.Create("General Department Team", DepartmentBranchSeeder.DefaultDepartmentId,
        managerId: null, id: DefaultTeamId));
}
// SaveChanges with the same DbUpdateException + retry-check block the DepartmentBranchSeeder has.
```

Register `TeamSeeder` (with `DepartmentBranchSeeder` first) in the Internal API's seeding composition,
and update `IdentitySeeder` so seeded staff carry the default org: set
`user.DepartmentId = DepartmentBranchSeeder.DefaultDepartmentId`,
`user.BranchId = DepartmentBranchSeeder.DefaultBranchId`, `user.TeamId = TeamSeeder.DefaultTeamId` on
the seeded administrator and agents (this is what makes `AC-512`'s "traversable from non-null data"
true for the default rows). `ApplicationUser` needs the setter — **this is Task 3's `AssignOrganisation`,
so run Task 3's domain method and call it from the seeder** (see Task 3; the ordering inside slice 0
is Task 1 → 2 → 3 → 4, and the seeder's last step depends on Task 3's method).

**Step 8 — the migration** (`AC-509`). Generate once after Tasks 1–4 (single migration for the whole
slice) and verify the hand-erased shape:

```
dotnet ef migrations add Phase2Enrichment --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi
dotnet build CustomerSupport.slnx
```

The `Up()` must contain: `CreateTable("Teams", ...DepartmentId FK Restrict...)`,
`AddColumn AspNetUsers.TeamId` (nullable), `AddColumn Tickets.TeamId` (nullable, FK Restrict),
`AddColumn Tickets.EscalationAssigneeId` (nullable, FK Restrict — Task 4), `AddColumn Tickets.{FirstResponseAt,LastResponseAt,ResolvedAt,ClosedAt}`
(nullable `datetime2`) — Task 2, and nothing destructive. Keep all existing rows valid: every new
column is nullable, so AC-509's "backfill to null, keep existing FKs valid" is automatic.

**Tests this task writes, naming their AC:**

| Test | AC | Level |
|---|---|---|
| `TC01_Team_Create_WithValidName` → `AC508_CreateTeam_WithValidName` | AC-508 | Unit |
| `TC02_Team_Deactivate_TogglesIsActive` → `AC508_DeactivateTeam_TogglesIsActive` | AC-508 | Unit |
| `AC508_CreateTeam_DuplicateNameInSameDepartment_Returns409` (unique `(DepartmentId, Name)`) | AC-508 | Integration |
| `AC508_CreateTeam_AdminOnly_Returns403ForAgent` | AC-508 | Integration |
| `TC03_Team_Migration_AddsFks_KeepsRows` → `AC509_Migration_AddsTeamFks_KeepsExistingRows` (asserts `AspNetUsers.TeamId`, `Tickets.TeamId` exist and seeded rows valid) | AC-509 | Integration |

---

### Task 2: Lifecycle timestamps — columns, `RecordResponse`, outbound stamping, DTOs (`US-906`; `AC-510`)

**Files:**
- `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs` — four new properties + `RecordResponse(DateTime)`
- `backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/RecordTicketMessageCommandHandler.cs` — tracked load + stamp
- `backend/src/CustomerSupport.Application/Features/Tickets/Dtos/TicketDtos.cs` — `TicketListItemDto` + `TicketDetailDto` gain the fields
- `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs` — map the fields
- `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQueryHandler.cs` — map the fields
- `backend/tests/...` — new unit + integration tests

**Step 1 — the failing test first** (`Tests/Unit/Domain/TicketTests.cs`):

```csharp
[Fact]
[Trait("AC", "510")]
public void Ticket_RecordResponse_SetsFirstAndLast()   // US-906 TC-01
{
    var ticket = NewTicket();
    var first = DateTime.UtcNow.AddMinutes(-5);

    ticket.RecordResponse(first);

    ticket.FirstResponseAt.Should().Be(first);
    ticket.LastResponseAt.Should().Be(first);

    var second = DateTime.UtcNow;
    ticket.RecordResponse(second);

    ticket.FirstResponseAt.Should().Be(first);   // first preserves
    ticket.LastResponseAt.Should().Be(second);   // last overwrites
}
```

**Step 2 — the aggregate.** Add to `Ticket.cs` (properties beside `PausedAt`, method near the SLA block):

```csharp
/// <summary>
/// Lifecycle timestamps for BI (US-906, AC-510, spec A5). Stamped, never derived: first/last
/// response by <see cref="RecordResponse"/>; resolved/closed on the transitions into those statuses
/// and cleared on reopen (Task 5 completes those). Null until the event happens — a report must
/// never read a zero.
/// </summary>
public DateTime? FirstResponseAt { get; private set; }
public DateTime? LastResponseAt { get; private set; }
public DateTime? ResolvedAt { get; private set; }
public DateTime? ClosedAt { get; private set; }

/// <summary>
/// AC-510/A5. Called on every outbound message; the first call sets both, later calls only move
/// <see cref="LastResponseAt"/>. One stamp, two consumers.
/// </summary>
public void RecordResponse(DateTime stampedAt)
{
    FirstResponseAt ??= stampedAt;
    LastResponseAt = stampedAt;
    MarkUpdated();
}
```

**Step 3 — the handler switches to a tracked load and stamps outbound** (`RecordTicketMessageCommandHandler.cs:28,34-38`).

Change `var ticket = await tickets.GetByIdAsync(request.TicketId, ct);` to
`var ticket = await tickets.GetTrackedAsync(request.TicketId, ct);` (the change must be saved in the
same unit of work). After the message is created and before `SaveChangesAsync`, stamp outbound:

```csharp
await messages.AddAsync(message, ct);

if (request.Direction == "Outbound")
{
    ticket.RecordResponse(DateTime.UtcNow);
}

await unitOfWork.SaveChangesAsync(ct);
```

The existing WhatsApp/SMS branch below stays untouched. Nothing further needed — the tracked ticket's
change is persisted by the same `SaveChangesAsync`.

**Step 4 — the DTOs and their mapping** (`AC-510` "surfaced in the ticket DTOs").

`TicketListItemDto` gains, before the trailing `EscalationState`:

```csharp
    // US-906 / AC-510. Null until the event happens.
    DateTime? FirstResponseAt,
    DateTime? LastResponseAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
```

`TicketDetailDto` gains the same four after `ResolutionDueAt`. In `GetTicketByIdQueryHandler` and
`GetTicketsQueryHandler` pass `ticket.FirstResponseAt, ticket.LastResponseAt, ticket.ResolvedAt,
ticket.ClosedAt` in the matching positions.

**Tests this task writes:**

| Test | AC | Level |
|---|---|---|
| `Ticket_RecordResponse_SetsFirstAndLast` (US-906 TC-01, above) | AC-510 | Unit |
| `FirstOutboundMessage_StampsTicket` (US-906 TC-04): record an outbound message via the endpoint, re-fetch detail, assert `firstResponseAt`/`lastResponseAt` set; record again, assert `lastResponseAt` moved and `firstResponseAt` unchanged | AC-510 | Integration |
| `AC510_ListRows_CarryLifecycleTimestamps` — the queue DTO carries the four fields | AC-510 | Integration |

`Ticket_ResolveClosed_Stamp_ClearOnReopen` (US-906 TC-02) is written in **Task 5**, where the
`Resolved`/`Closed` transitions and the reopen that clears them land — the task depends on the
machine, not this one.

---

### Task 3: Org-chain wiring — dormant columns populated (`US-907`; `AC-511`, `AC-512`)

**Files:**
- `backend/src/CustomerSupport.Domain/Entities/Identity/ApplicationUser.cs` — `AssignOrganisation`
- `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs` — `InheritOrganisation`
- `backend/src/CustomerSupport.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs` — inherit actor org
- `backend/src/CustomerSupport.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommandHandler.cs` — inherit assignee org
- `backend/src/CustomerSupport.Application/Features/Users/Commands/UpdateUser/UpdateUserRequest.cs`, `UpdateUserCommand.cs`, `UpdateUserCommandHandler.cs` — admin sets a user's org
- `backend/tests/...` — new unit + integration tests

**Step 1 — the failing tests.** `Tests/Unit/Domain/TicketTests.cs`:

```csharp
[Fact]
[Trait("AC", "511")]
public void Ticket_Assign_PropagatesOrg()   // US-907 TC-01
{
    var ticket = NewTicket();
    var org = ("dddddddd-dddd-dddd-dddd-ddddddddddd1",
               "dddddddd-dddd-dddd-dddd-ddddddddddd2",
               "dddddddd-dddd-dddd-dddd-ddddddddddd3");

    ticket.InheritOrganisation(org.Item1, org.Item2, org.Item3);

    ticket.DepartmentId.Should().Be(org.Item1);
    ticket.BranchId.Should().Be(org.Item2);
    ticket.TeamId.Should().Be(org.Item3);
}
```

Integration `CreateTicket_InheritsActingAgentOrg` (US-907 TC-02): seed an agent whose
`DepartmentId`/`BranchId`/`TeamId` are the defaults, create a ticket through the endpoint, load the
detail, assert the three org fields reflect the actor's values.

**Step 2 — the aggregate method** (`Ticket.cs`, after `AssignTo`):

```csharp
/// <summary>
/// US-907 / AC-511. Populates the dormant organisational columns — from the assignee on assign,
/// from the acting agent at creation (A7). Nulls mean "not wired", never a default.
/// </summary>
public void InheritOrganisation(Guid? departmentId, Guid? branchId, Guid? teamId)
{
    DepartmentId = departmentId;
    BranchId = branchId;
    TeamId = teamId;
    MarkUpdated();
}
```

**Step 3 — the user setter** (`ApplicationUser.cs`, after `UpdateProfile`):

```csharp
/// <summary>
/// US-907 / AC-511. Wires the user into the org drill-down. Admin-managed via the UpdateUser
/// surface; also used by the seeder so seeded staff sit in the default org.
/// </summary>
public void AssignOrganisation(Guid? departmentId, Guid? branchId, Guid? teamId)
{
    DepartmentId = departmentId;
    BranchId = branchId;
    TeamId = teamId;
    UpdatedAt = DateTime.UtcNow;
}
```

**Step 4 — creation inherits the actor's org** (`CreateTicketCommandHandler`). The handler already
constructs the aggregate from `userContext.UserId`; inject `IIdentityUserService identityUsers` and,
right after `Ticket.Create(...)`:

```csharp
var actor = await identityUsers.FindByIdAsync(userContext.UserId, ct);
if (actor is not null)
{
    ticket.InheritOrganisation(actor.DepartmentId, actor.BranchId, actor.TeamId);
}
```

(Only when the actor has them — nullable props pass through as null for portals/unwired staff.)

**Step 5 — assignment inherits the assignee's org** (`AssignTicketCommandHandler`). After the
`target.IsActive` check and before `ticket.AssignTo(...)`:

```csharp
ticket.InheritOrganisation(target.DepartmentId, target.BranchId, target.TeamId);
ticket.AssignTo(request.AssigneeId, userContext.UserId);
```

**Step 6 — the update-user surface.** `UpdateUserRequest` and `UpdateUserCommand` gain
`Guid? DepartmentId, Guid? BranchId, Guid? TeamId`; `UpdateUserCommandHandler` calls, after
`UpdateProfile`:

```csharp
user.AssignOrganisation(request.DepartmentId, request.BranchId, request.TeamId);
```

**Step 7 — seed the org onto staff.** Update `IdentitySeeder` to call `AssignOrganisation` with the
default ids once the three exist (see Task 1 step 7). This is the concrete half of `AC-512`.

**Tests this task writes:**

| Test | AC | Level |
|---|---|---|
| `Ticket_Assign_PropagatesOrg` (US-907 TC-01) | AC-511 | Unit |
| `CreateTicket_InheritsActingAgentOrg` (US-907 TC-02) | AC-511 | Integration |
| `AC511_Assign_InheritsAssigneeOrg` — assignment writes the assignee's dept/branch/team onto the ticket | AC-511 | Integration |
| `AC512_DefaultOrg_Traversable` — the seeded admin/agent rows carry non-null `Dept/Branch/Team`, resolvable forward | AC-512 | Integration |

---

### Task 4: Escalation owner — `TakeEscalation` + handoff endpoint + DTO (`US-904`; `AC-506`, and the `AC-507` marker half that is not frontend)

**Files:**
- `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs` — `EscalationAssigneeId` + `TakeEscalation`
- `backend/src/CustomerSupport.Application/Features/Tickets/Commands/TakeEscalation/{Command,Validator,Handler}.cs` — new
- `backend/src/CustomerSupport.Application/Features/Tickets/Dtos/TicketDtos.cs` — `EscalationAssigneeId` (+ detail `EscalationAssigneeName`)
- `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs` — owner name
- `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` — `POST {id}/escalation-owner`
- `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs` + `SystemCodeMap` + `SystemCode` + `Resources.yaml` — escalation keys
- `backend/tests/...` — new unit + integration tests

**Step 1 — the failing test** (`Tests/Unit/Domain/TicketTests.cs`) — US-904 TC-01:

```csharp
[Fact]
[Trait("AC", "506")]
public void Ticket_TakeEscalation_SetsOwner_RecordsHistory()
{
    var ticket = AssignedTicketAt("In Progress");
    ticket.Escalate("Level1");

    ticket.TakeEscalation(Specialist, Supervisor);

    ticket.EscalationAssigneeId.Should().Be(Specialist);
    ticket.EscalationState.Should().Be("Level1");   // the level already set is reflected
    var entry = ticket.History.Last();
    entry.ChangeType.Should().Be("Escalated");
    entry.FromValue.Should().BeNull();
    entry.ToValue.Should().Be(Specialist.ToString());

    ticket.TakeEscalation(OtherSpecialist, Supervisor);   // a second hand-off appends another row
    ticket.History.Last().ChangeType.Should().Be("Escalated");
    ticket.History.Last().FromValue.Should().Be(Specialist.ToString());
    ticket.History.Last().ToValue.Should().Be(OtherSpecialist.ToString());
}
```

**Step 2 — the aggregate.** Add to `Ticket.cs` (property beside `EscalationState`, method beside
`AdvanceEscalation`):

```csharp
/// <summary>
/// The Supervisor/Specialist holding an escalated ticket (US-904, AC-506). Null while the ticket is
/// not escalated. A marker field beside <see cref="EscalationState"/> — escalation is never a status.
/// </summary>
public Guid? EscalationAssigneeId { get; private set; }

/// <summary>
/// US-904 / AC-506. Hands the escalated ticket to a named owner, recording an <c>Escalated</c>
/// history row per hand-off (append-only, AC-48). The escalation *level* is untouched — it is the
/// scanner's field; this names who is doing the work.
/// </summary>
public void TakeEscalation(Guid specialistId, Guid actorId)
{
    if (specialistId == Guid.Empty)
    {
        throw new ArgumentException("An escalation owner is required", nameof(specialistId));
    }

    if (actorId == Guid.Empty)
    {
        throw new ArgumentException("An actor is required", nameof(actorId));
    }

    if (EscalationState == "None")
    {
        throw new InvalidOperationException($"Ticket '{Reference}' is not escalated and has no owner to take.");
    }

    if (EscalationAssigneeId == specialistId)
    {
        throw new InvalidOperationException($"Ticket '{Reference}' is already held by that owner.");
    }

    var previous = EscalationAssigneeId;

    EscalationAssigneeId = specialistId;
    MarkUpdated();
    UpdatedBy = actorId;

    Append(actorId, TicketChangeType.Escalated, previous?.ToString(), specialistId.ToString());
}
```

**Step 3 — the handler.** `TakeEscalationCommand(Guid TicketId, Guid AssigneeId, string RowVersion)`
+ request `TakeEscalationRequest(Guid AssigneeId, string RowVersion)`. The handler mirrors
`AssignTicketCommandHandler`'s validation chain (assignee exists, is an active `Agent`) then:

```csharp
ticket.TakeEscalation(request.AssigneeId, userContext.UserId);
```

set the `RowVersion` original value, save, and translate `InvalidOperationException` refusals
(not-escalated, already-owner) to `MessageType.Conflict` (no new failure codes — D2). Success is a
new key `ApplicationErrors.Ticket.ESCALATION_OWNER_SET` (`CON073`, with En/Ar strings).

**Step 4 — the endpoint** (`TicketsController.cs`):

```csharp
/// <summary>Hands an escalated ticket to a named Specialist/Supervisor (US-904, AC-506).</summary>
[HttpPost("{id:guid}/escalation-owner")]
[Authorize(Policy = "Supervisor")]
[ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status409Conflict)]
public async Task<IActionResult> TakeEscalation(Guid id, [FromBody] TakeEscalationRequest request, CancellationToken ct)
{
    var result = await mediator.Send(new TakeEscalationCommand(id, request.AssigneeId, request.RowVersion), ct);
    return this.ToActionResult(result);
}
```

**Step 5 — the DTOs.** `TicketListItemDto` and `TicketDetailDto` each gain
`Guid? EscalationAssigneeId`; `TicketDetailDto` additionally gains `string? EscalationAssigneeName`.
`GetTicketByIdQueryHandler` resolves the name like it resolves `assigneeName`. This is the
`AC-507` backend half: the marker data is on every ticket payload so a screen can render level +
owner without a second request. The *rendering* itself (banner, hand-off UI) ships US-920 / `FEAT-30`.

**Tests this task writes:**

| Test | AC | Level |
|---|---|---|
| `Ticket_TakeEscalation_SetsOwner_RecordsHistory` (US-904 TC-01) | AC-506 | Unit |
| `AC506_TakeEscalation_OnNonEscalatedTicket_Returns409` | AC-506 | Integration |
| `AC506_TakeEscalation_Endpoint_SetsOwner_AndDetailShowsIt` (supervisor takes an escalated ticket; detail returns `escalationAssigneeId`+name) | AC-506/507 | Integration |
| `AC506_TakeEscalation_AgentCaller_Returns403` | AC-506 | Integration |
| `TicketStatus_All_DoesNotIncludeEscalated` (US-904 TC-02) is claimed in Task 5 — it is a property of the new `All` list. | AC-507 | Unit |

**Slice 0 exit:** `dotnet test CustomerSupport.slnx` green with every test above passing and naming
its `AC`.

---

## Slice 1 — the 8-state lifecycle and the shared frontend status model

### Task 5: The 8-state machine — `TicketStatus`, `ChangeStatus`, reopen, resolve/close stamps, data migration (`US-901`; `AC-501`, `AC-502`, `AC-503`, plus the `AC-510` stamp/clear and `AC-507` "never a status")

**Files:**
- `backend/src/CustomerSupport.Domain/ValueObjects/TicketStatus.cs` — replace entire file
- `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs` — `ChangeStatus` add stamps/guard/reopen clear
- `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketConfiguration.cs` — Status `HasMaxLength(16)`→`32`
- `backend/src/CustomerSupport.Infrastructure/Persistence/Migrations/` — `Phase2Workflow` migration
- `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` — XML doc param update (line 45)
- `backend/tests/CustomerSupport.Tests/Unit/Domain/TicketStatusTests.cs` — replace entire file
- `backend/tests/CustomerSupport.Tests/Unit/Domain/TicketTests.cs` — AC-503/AC-510 entity tests
- `backend/tests/CustomerSupport.Tests/Integration/TicketLifecycleEndpointTests.cs` — AC37/40/41/48 rework
- `backend/tests/CustomerSupport.Tests/Integration/SlaPauseAndEscalationEndpointTests.cs` — AC134-136 walk rework
- `backend/tests/CustomerSupport.Tests/Integration/AutoEscalationEndpointTests.cs` — AC2183 InlineData rework
- `backend/tests/CustomerSupport.Tests/Integration/SlaTrackingEndpointTests.cs` — AC133 direct-write rework

**Step 1 — the failing unit tests first.**

Replace `Tests/Unit/Domain/TicketStatusTests.cs` entirely:

```csharp
public class TicketStatusTests
{
    /// <summary>The 12 legal pairs of the 8-state machine (AC-501).</summary>
    public static TheoryData<string, string> PermittedTransitions => new()
    {
        { "New", "Open" },
        { "Open", "Assigned" },
        { "Open", "Resolved" },
        { "Assigned", "In Progress" },
        { "In Progress", "Waiting for Customer" },
        { "In Progress", "Waiting for Internal Team" },
        { "In Progress", "Resolved" },
        { "Waiting for Customer", "In Progress" },
        { "Waiting for Internal Team", "In Progress" },
        { "Resolved", "In Progress" },
        { "Resolved", "Closed" },
        { "Closed", "In Progress" },
    };

    [Theory]
    [MemberData(nameof(PermittedTransitions))]
    [Trait("AC", "501")]
    public void TicketStatus_AllowsEachLegalTransition(string from, string to)   // US-901 TC-01
    {
        TicketStatus.Create(from).CanTransitionTo(TicketStatus.Create(to)).Should().BeTrue();
    }

    /// <summary>All 64 pairs minus the 12 permitted — 52 refusals including every self-transition.</summary>
    public static TheoryData<string, string> RefusedTransitions
    {
        get
        {
            var data = new TheoryData<string, string>();
            var all = TicketStatus.All.Select(s => s.Value).ToArray();
            var permitted = new HashSet<(string, string)>(
                PermittedTransitions.Select(t => ((string)t[0], (string)t[1])));
            foreach (var from in all)
            foreach (var to in all)
            {
                if (!permitted.Contains((from, to)))
                {
                    data.Add(from, to);
                }
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(RefusedTransitions))]
    [Trait("AC", "502")]
    public void TicketStatus_RefusesEveryIllegalTransition(string from, string to)   // US-901 TC-02
    {
        TicketStatus.Create(from).CanTransitionTo(TicketStatus.Create(to)).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "502")]
    public void Create_RejectsStatusesOutsideTheEight()   // guards 400-vs-409 (AC-30 survives)
    {
        var act = () => TicketStatus.Create("Pending");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "507")]
    public void TicketStatus_All_DoesNotIncludeEscalated()   // US-904 TC-02
    {
        TicketStatus.All.Should().HaveCount(8);
        TicketStatus.All.Select(s => s.Value).Should().NotContain("Escalated");
    }
}
```

Entity-level tests in `Tests/Unit/Domain/TicketTests.cs`:

```csharp
[Fact]
[Trait("AC", "503")]
public void Ticket_Reopening_RecordsReopenedHistory()   // US-901 TC-03
{
    var ticket = TicketAt("In Progress", assigned: true);
    ticket.ChangeStatus("Resolved", Agent);

    ticket.ChangeStatus("In Progress", Agent);   // AC-503 reopen → In Progress

    ticket.Status.Should().Be("In Progress");
    var entry = ticket.History.Last();
    entry.ChangeType.Should().Be("Reopened");
    entry.FromValue.Should().Be("Resolved");
    entry.ToValue.Should().Be("In Progress");
}

[Fact]
[Trait("AC", "510")]
public void Ticket_Resolve_Close_StampAndReopenClears()   // US-906 TC-02
{
    var ticket = TicketAt("In Progress", assigned: true);

    ticket.ChangeStatus("Resolved", Agent);
    ticket.ResolvedAt.Should().NotBeNull();
    ticket.ClosedAt.Should().BeNull();

    ticket.ChangeStatus("Closed", Agent);
    ticket.ClosedAt.Should().NotBeNull();

    ticket.ChangeStatus("In Progress", Agent);   // reopen, AC-503
    ticket.ResolvedAt.Should().BeNull();
    ticket.ClosedAt.Should().BeNull();
    ticket.Status.Should().Be("In Progress");
}
```

**Step 2 — the replacement `TicketStatus.cs`.**

Replace the entire file with:

```csharp
namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// The eight ticket lifecycle states and the closed table of transitions between them
/// (AC-501, AC-502, AC-503; supersedes the five-state AC-37..AC-40 table). Persisted as a string,
/// never as an int: reordering this type must not renumber existing rows. Escalation is a marker
/// (<see cref="EscalationState"/>/<see cref="EscalationAssigneeId"/>), never a status (AC-507) —
/// it is deliberately absent from <see cref="All"/>.
/// </summary>
public sealed class TicketStatus : ValueObject
{
    public string Value { get; }

    public static readonly TicketStatus New = new("New");
    public static readonly TicketStatus Open = new("Open");
    public static readonly TicketStatus Assigned = new("Assigned");
    public static readonly TicketStatus InProgress = new("In Progress");
    public static readonly TicketStatus WaitingForCustomer = new("Waiting for Customer");
    public static readonly TicketStatus WaitingForInternalTeam = new("Waiting for Internal Team");
    public static readonly TicketStatus Resolved = new("Resolved");
    public static readonly TicketStatus Closed = new("Closed");

    /// <summary>Every status, in lifecycle order. Escalated is not here — escalation is a marker (AC-507).</summary>
    public static IReadOnlyList<TicketStatus> All { get; } =
        [New, Open, Assigned, InProgress, WaitingForCustomer, WaitingForInternalTeam, Resolved, Closed];

    private TicketStatus(string value)
    {
        Value = value;
    }

    public static TicketStatus Create(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status is required", nameof(status));
        }

        return status.Trim() switch
        {
            "New" => New,
            "Open" => Open,
            "Assigned" => Assigned,
            "In Progress" => InProgress,
            "Waiting for Customer" => WaitingForCustomer,
            "Waiting for Internal Team" => WaitingForInternalTeam,
            "Resolved" => Resolved,
            "Closed" => Closed,
            _ => throw new ArgumentException(
                $"Invalid ticket status: {status}. Must be one of the eight lifecycle statuses (AC-501).",
                nameof(status))
        };
    }

    public static bool TryCreate(string? status, out TicketStatus? result, out string? error)
    {
        try
        {
            result = Create(status);
            error = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            result = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// The transition table (AC-501). Everything not listed is refused, and the diagonal is
    /// deliberately empty — a ticket cannot transition to a status it already holds (AC-39/AC-502).
    /// </summary>
    public bool CanTransitionTo(TicketStatus target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return (Value, target.Value) switch
        {
            ("New", "Open") => true,
            ("Open", "Assigned") => true,
            ("Open", "Resolved") => true,
            ("Assigned", "In Progress") => true,
            ("In Progress", "Waiting for Customer") => true,
            ("In Progress", "Waiting for Internal Team") => true,
            ("In Progress", "Resolved") => true,
            ("Waiting for Customer", "In Progress") => true,
            ("Waiting for Internal Team", "In Progress") => true,
            ("Resolved", "In Progress") => true,   // reopen, AC-503
            ("Resolved", "Closed") => true,
            ("Closed", "In Progress") => true,     // reopen, AC-503
            _ => false
        };
    }

    /// <summary>
    /// True when moving to <paramref name="target"/> is a reopen rather than ordinary progress —
    /// which history records under its own change type (AC-503). Uses <see cref="Value"/> equality
    /// rather than reference identity so the check is reliable for any two created instances.
    /// </summary>
    public bool IsReopenTo(TicketStatus target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.Value == InProgress.Value
            && (Value == Resolved.Value || Value == Closed.Value);
    }

    /// <summary>A work state requires an assignee (AC-505).</summary>
    public bool IsWorkState() =>
        Value is "In Progress" or "Waiting for Customer" or "Waiting for Internal Team";

    public bool IsNew => Value == New.Value;
    public bool IsOpen => Value == Open.Value;
    public bool IsAssigned => Value == Assigned.Value;
    public bool IsInProgress => Value == InProgress.Value;
    public bool IsWaitingForCustomer => Value == WaitingForCustomer.Value;
    public bool IsWaitingForInternalTeam => Value == WaitingForInternalTeam.Value;
    public bool IsResolved => Value == Resolved.Value;
    public bool IsClosed => Value == Closed.Value;

    public static implicit operator string(TicketStatus status) => status.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

**Step 3 — `Ticket.ChangeStatus` adds stamps and the AC-505 guard.**

Edit `Ticket.cs:152-180` (`ChangeStatus` method body). Replace the guard block (after `CanTransitionTo` check) and add the lifecycle stamps:

```csharp
public void ChangeStatus(string targetStatus, Guid actorId)
{
    if (actorId == Guid.Empty)
    {
        throw new ArgumentException("An actor is required", nameof(actorId));
    }

    var current = TicketStatus.Create(Status);
    var target = TicketStatus.Create(targetStatus);

    if (!current.CanTransitionTo(target))
    {
        throw new InvalidOperationException(
            $"Cannot change ticket status from '{current.Value}' to '{target.Value}'.");
    }

    // AC-505: a work state cannot be entered without an assignee. The guard is in the aggregate so
    // the existing handler pre-check surfaces it as a 409 without adding a new refusal shape (D2).
    if (target.IsWorkState() && AssigneeId is null)
    {
        throw new InvalidOperationException(
            $"Ticket '{Reference}' must be assigned before it can be '{target.Value}'.");
    }

    var isReopen = current.IsReopenTo(target);
    var changeType = isReopen ? TicketChangeType.Reopened : TicketChangeType.StatusChanged;

    // US-906 / AC-510: entering Resolved/Closed stamps the respective timestamp; reopening clears
    // both so the next resolve starts clean.
    if (isReopen)
    {
        ResolvedAt = null;
        ClosedAt = null;
    }
    else
    {
        if (target.Value == "Resolved") ResolvedAt = DateTime.UtcNow;
        if (target.Value == "Closed") ClosedAt = DateTime.UtcNow;
    }

    Status = target.Value;
    MarkUpdated();
    UpdatedBy = actorId;

    ApplySlaPauseTransition(current.Value, target.Value);

    Append(actorId, changeType, current.Value, target.Value);
    AddDomainEvent(new TicketStatusChangedEvent(Id, Reference, current.Value, target.Value, actorId));
}
```

**Step 4 — `TicketConfiguration` widens the Status column.**

Edit `TicketConfiguration.cs:24`: `HasMaxLength(16)` → `HasMaxLength(32)`.

**Step 5 — the `Phase2Workflow` migration.**

```powershell
dotnet ef migrations add Phase2Workflow --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi
```

The `Up()` must:
1. `AlterColumn("Tickets", "Status", x => x.Property<string>(maxLength: 32, required: true));`
2. `UPDATE Tickets SET Status = 'Waiting for Customer' WHERE Status = 'Pending';`

The `Down()` reverses the UPDATE, then the column shrink.

**Step 6 — `TicketsController` XML doc update.**

Edit `TicketsController.cs:45`: change the `<param name="status">` documentation from the five old statuses to the eight new ones.

**Step 7 — integration test rewires** (full file-level edits described in the Test Rework Appendix below).

**Tests this task writes:**

| Test | AC | Level |
|---|---|---|
| `TicketStatus_AllowsEachLegalTransition` (12 pairs, US-901 TC-01) | AC-501 | Unit |
| `TicketStatus_RefusesEveryIllegalTransition` (52 pairs, US-901 TC-02) | AC-502 | Unit |
| `Create_RejectsStatusesOutsideTheEight` (guards 400-vs-409) | AC-502 | Unit |
| `TicketStatus_All_DoesNotIncludeEscalated` (US-904 TC-02) | AC-507 | Unit |
| `Ticket_Reopening_RecordsReopenedHistory` (US-901 TC-03) | AC-503 | Unit |
| `Ticket_Resolve_Close_StampAndReopenClears` (US-906 TC-02) | AC-510 | Unit |
| `AC501_ChangeStatus_8StateMachine_PermittedTransition_Returns200` (TicketLifecycleEndpointTests) | AC-501 | Integration |
| `AC503_Reopening_RecordsReopenedRow_AndSetsStatusToInProgress` (TicketLifecycleEndpointTests) | AC-503 | Integration |
| `AC510_LifecycleTimestamps_ResolvedAt_ClosedAt_StampAndClearOnReopen` (TicketLifecycleEndpointTests) | AC-510 | Integration |

---

### Task 6: Assignment required before work (`US-903` AC1; `AC-505`) — aggregate guard + handler 409

**Files:**
- `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs` — the AC-505 guard in `ChangeStatus` (added in Task 5)
- `backend/src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommandHandler.cs` — no change needed (existing `InvalidOperationException`→`MessageType.Conflict` map handles it)
- `backend/tests/CustomerSupport.Tests/Unit/Domain/TicketTests.cs` — `TicketAt` helper rework + failing guard tests
- `backend/tests/CustomerSupport.Tests/Integration/TicketLifecycleEndpointTests.cs` — `TicketAtAsync` rework + AC-505 integration tests

**Step 1 — the failing unit tests.**

In `Tests/Unit/Domain/TicketTests.cs`, update the `TicketAt` helper and add the AC-505 guard tests:

```csharp
/// <summary>
/// Builds a ticket in the named status, optionally ensuring it has an assignee (required for
/// entering work states per AC-505).
/// </summary>
private static Ticket TicketAt(string status, bool assigned = false)
{
    var ticket = new Ticket(
        subject: "Test ticket",
        description: "Test description",
        customerId: Guid.NewGuid(),
        categoryId: Guid.NewGuid(),
        priority: "Normal",
        actorId: Agent);

    // Assign early when the target status is a work state, because AC-505 requires it.
    if (assigned || status is "In Progress" or "Waiting for Customer" or "Waiting for Internal Team")
    {
        ticket.AssignTo(Agent, Supervisor);
    }

    string[] path = status switch
    {
        "New" => [],
        "Open" => ["Open"],
        "Assigned" => ["Open", "Assigned"],
        "In Progress" => ["Open", "Assigned", "In Progress"],
        "Waiting for Customer" => ["Open", "Assigned", "In Progress", "Waiting for Customer"],
        "Waiting for Internal Team" => ["Open", "Assigned", "In Progress", "Waiting for Internal Team"],
        "Resolved" => ["Open", "Resolved"],
        "Closed" => ["Open", "Resolved", "Closed"],
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    foreach (var step in path)
    {
        ticket.ChangeStatus(step, Agent);
    }

    return ticket;
}

[Fact]
[Trait("AC", "505")]
public void Ticket_EnteringWorkState_WithoutAssignee_Throws()   // US-903 AC1
{
    var ticket = TicketAt("Assigned", assigned: false);   // status is "Assigned" but no assignee set

    var act = () => ticket.ChangeStatus("In Progress", Agent);

    act.Should().Throw<InvalidOperationException>()
        .WithMessage("*must be assigned*");
    ticket.Status.Should().Be("Assigned");
}

[Fact]
[Trait("AC", "505")]
public void Ticket_EnteringWorkState_WhenAssigned_Proceeds()
{
    var ticket = TicketAt("Assigned", assigned: true);

    ticket.ChangeStatus("In Progress", Agent);

    ticket.Status.Should().Be("In Progress");
}

[Theory]
[Trait("AC", "505")]
[InlineData("In Progress")]
[InlineData("Waiting for Customer")]
[InlineData("Waiting for Internal Team")]
public void Ticket_WorkStates_RequireAssignee(string workStatus)
{
    var ticket = TicketAt("Assigned", assigned: false);

    var act = () => ticket.ChangeStatus(workStatus, Agent);

    act.Should().Throw<InvalidOperationException>();
}
```

**Step 2 — the `TicketAtAsync` helper in the integration test** (`TicketLifecycleEndpointTests.cs:106-125`).

Replace the `TicketAtAsync` method body:

```csharp
private async Task<Guid> TicketAtAsync(string status)
{
    var id = await CreateTicketAsync();

    // Assign before entering any work state (AC-505). Supervisor can assign any ticket (AC-42).
    var needsAssignee = status is "In Progress" or "Waiting for Customer" or "Waiting for Internal Team";
    if (needsAssignee)
    {
        (await AssignAsync(_supervisor, id, _agentUser.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    string[] path = status switch
    {
        "New" => [],
        "Open" => ["Open"],
        "Assigned" => ["Open", "Assigned"],
        "In Progress" => ["Open", "Assigned", "In Progress"],
        "Waiting for Customer" => ["Open", "Assigned", "In Progress", "Waiting for Customer"],
        "Waiting for Internal Team" => ["Open", "Assigned", "In Progress", "Waiting for Internal Team"],
        "Resolved" => ["Open", "Resolved"],
        "Closed" => ["Open", "Resolved", "Closed"],
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    foreach (var step in path)
    {
        (await ChangeStatusAsync(_supervisor, id, step)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    return id;
}
```

**Step 3 — the handler is already correct.** The existing
`catch (InvalidOperationException)` → `MessageType.Conflict` chain in
`ChangeTicketStatusCommandHandler.cs:44-47` handles the guard automatically; no code change required.

**Step 4 — integration tests for AC-505.**

Add to `TicketLifecycleEndpointTests.cs`:

```csharp
[Fact]
[Trait("AC", "505")]
public async Task AC505_UnassignedTicket_EnteringWorkState_Returns409()
{
    var id = await TicketAtAsync("Assigned");   // walked to Assigned but never assigned
    var response = await ChangeStatusAsync(_supervisor, id, "In Progress");

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    var body = await response.Content.ReadFromJsonAsync<Response<object>>();
    body!.Code.Should().Be(SystemCode.ERR013);   // TRANSITION_NOT_ALLOWED — same code as AC-38 (D2)
}

[Theory]
[Trait("AC", "505")]
[InlineData("Resolved")]
[InlineData("Closed")]
public async Task AC505_ReopeningUnassignedTicket_Returns409(string from)
{
    var id = await TicketAtAsync(from);   // walked without assignment; reopening needs assignee
    var response = await ChangeStatusAsync(_supervisor, id, "In Progress");

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}
```

**Tests this task writes:**

| Test | AC | Level |
|---|---|---|
| `Ticket_EnteringWorkState_WithoutAssignee_Throws` | AC-505 | Unit |
| `Ticket_EnteringWorkState_WhenAssigned_Proceeds` | AC-505 | Unit |
| `Ticket_WorkStates_RequireAssignee` (3 states) | AC-505 | Unit |
| `AC505_UnassignedTicket_EnteringWorkState_Returns409` | AC-505 | Integration |
| `AC505_ReopeningUnassignedTicket_Returns409` (2 states) | AC-505 | Integration |

---

### Task 7: SLA pause keys on both waiting states (`US-902`; `AC-504`)

**Files:**
- `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs` — `ApplySlaPauseTransition` re-key
- `backend/src/CustomerSupport.Infrastructure/Jobs/SlaBreachScanner.cs` — `EvaluatedStatuses` expand + comment
- `backend/tests/CustomerSupport.Tests/Integration/SlaPauseAndEscalationEndpointTests.cs` — AC134-136 walk rework
- `backend/tests/CustomerSupport.Tests/Integration/AutoEscalationEndpointTests.cs` — AC2183 InlineData + walk
- `backend/tests/CustomerSupport.Tests/Integration/SlaTrackingEndpointTests.cs` — AC133 direct-write update

**Step 1 — the failing unit test.**

```csharp
[Fact]
[Trait("AC", "504")]
public void Ticket_SlaPause_WaitingForCustomer_ShiftsDueDates()
{
    var ticket = TicketAt("In Progress", assigned: true);
    var originalDue = DateTime.UtcNow.AddHours(4);
    ticket.ResponseDueAt = originalDue;
    ticket.ResolutionDueAt = originalDue;

    ticket.ChangeStatus("Waiting for Customer", Agent);   // pause starts
    var pausedAt = ticket.PausedAt;
    pausedAt.Should().NotBeNull();

    ticket.ChangeStatus("In Progress", Agent);   // resume — due dates shift by elapsed

    ticket.PausedAt.Should().BeNull();
    ticket.TotalPausedSeconds.Should().BeGreaterThan(0);
    ticket.ResponseDueAt.Should().BeAfter(originalDue);
    ticket.ResolutionDueAt.Should().BeAfter(originalDue);
}
```

**Step 2 — `ApplySlaPauseTransition` re-key.**

Replace `Ticket.cs:187-213` (`ApplySlaPauseTransition` body):

```csharp
private void ApplySlaPauseTransition(string fromStatus, string toStatus)
{
    // AC-504: both "Waiting for Customer" and "Waiting for Internal Team" pause the SLA.
    // Entering either starts the pause; leaving either (back to "In Progress") accumulates
    // the elapsed span and shifts both due dates forward by that span.
    bool isWaitingStatus(string s) =>
        s is "Waiting for Customer" or "Waiting for Internal Team";

    if (isWaitingStatus(toStatus) && PausedAt is null)
    {
        PausedAt = DateTime.UtcNow;
        return;
    }

    if (isWaitingStatus(fromStatus) && toStatus != fromStatus && PausedAt is { } pausedAt)
    {
        var elapsed = DateTime.UtcNow - pausedAt;
        TotalPausedSeconds += (int)Math.Max(0, elapsed.TotalSeconds);
        PausedAt = null;

        if (ResponseDueAt is { } responseDue)
        {
            ResponseDueAt = responseDue.Add(elapsed);
        }

        if (ResolutionDueAt is { } resolutionDue)
        {
            ResolutionDueAt = resolutionDue.Add(elapsed);
        }
    }
}
```

**Step 3 — `SlaBreachScanner` `EvaluatedStatuses` expand.**

Edit `SlaBreachScanner.cs:44`:

```csharp
// Before:
private static readonly string[] EvaluatedStatuses = ["New", "Open"];

// After:
private static readonly string[] EvaluatedStatuses = ["New", "Open", "Waiting for Customer", "Waiting for Internal Team"];
```

Also update the comment at lines 38-43 to replace "Pending" references with "waiting states".

**Step 4 — `SlaPauseAndEscalationEndpointTests` AC134-136 walk rework.**

Add `_agentId` field and `AssignAsync` helper, then rewrite AC134/135/136:

```csharp
private Guid _agentId;

public async Task InitializeAsync()
{
    // ... existing init (add after _admin setup) ...
    (_agent, var agentUser) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
    _agentId = agentUser.Id;
}

private async Task AssignAsync(Guid ticketId, Guid agentId)
{
    var response = await _admin.PostAsJsonAsync(
        $"/api/Tickets/{ticketId}/assignee",
        new { assigneeId = agentId, rowVersion = await RowVersionAsync(ticketId) });
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
[Trait("AC", "134")]
public async Task AC134_TransitionToWaitingForCustomer_SetsPausedAt()
{
    var ticketId = await CreateTicketAsync();
    // New → Open → assign → Assigned → In Progress → Waiting for Customer
    await ChangeStatusAsync(ticketId, "Open");
    await AssignAsync(ticketId, _agentId);
    await WalkToAsync(ticketId, "Assigned", "In Progress", "Waiting for Customer");

    var ticket = await LoadTicketAsync(ticketId);
    ticket.PausedAt.Should().NotBeNull();
    ticket.TotalPausedSeconds.Should().Be(0);
}

[Fact]
[Trait("AC", "135")]
public async Task AC135_ExitingWaitingForCustomer_AccumulatesPausedSecondsAndShiftsDueDates()
{
    var ticketId = await CreateTicketAsync();
    var originalDue = DateTime.UtcNow.AddHours(4);
    using (var scope = _factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);
        db.Entry(ticket).Property(t => t.ResolutionDueAt).CurrentValue = originalDue;
        await db.SaveChangesAsync();
    }

    await ChangeStatusAsync(ticketId, "Open");
    await AssignAsync(ticketId, _agentId);
    await WalkToAsync(ticketId, "Assigned", "In Progress", "Waiting for Customer");
    await Task.Delay(1100);   // measurable pause
    await ChangeStatusAsync(ticketId, "In Progress");   // resume

    var ticket2 = await LoadTicketAsync(ticketId);
    ticket2.PausedAt.Should().BeNull();
    ticket2.TotalPausedSeconds.Should().BeGreaterThan(0);
    ticket2.ResolutionDueAt.Should().NotBeNull();
    ticket2.ResolutionDueAt!.Value.Should().BeAfter(originalDue);
}

[Fact]
[Trait("AC", "136")]
public async Task AC136_MultipleWaitingCycles_AccumulatePausedSeconds()
{
    var ticketId = await CreateTicketAsync();
    await ChangeStatusAsync(ticketId, "Open");
    await AssignAsync(ticketId, _agentId);
    await WalkToAsync(ticketId, "Assigned", "In Progress");

    // First cycle: enter WFC, pause, exit to In Progress
    await ChangeStatusAsync(ticketId, "Waiting for Customer");
    await Task.Delay(1100);
    await ChangeStatusAsync(ticketId, "In Progress");
    var afterFirst = (await LoadTicketAsync(ticketId)).TotalPausedSeconds;

    // Second cycle
    await ChangeStatusAsync(ticketId, "Waiting for Customer");
    await Task.Delay(1100);
    await ChangeStatusAsync(ticketId, "In Progress");
    var afterSecond = (await LoadTicketAsync(ticketId)).TotalPausedSeconds;

    afterFirst.Should().BeGreaterThan(0);
    afterSecond.Should().BeGreaterThan(afterFirst);
}
```

Add a `WalkToAsync` helper:

```csharp
private async Task WalkToAsync(Guid ticketId, params string[] steps)
{
    foreach (var step in steps)
    {
        await ChangeStatusAsync(ticketId, step);
    }
}
```

**Step 5 — `AutoEscalationEndpointTests` AC2183 InlineData + walk rework.**

Edit `AutoEscalationEndpointTests.cs:205-236`:

```csharp
// InlineData — replace "Pending" with "Waiting for Customer"
[Theory]
[Trait("AC", "218.3")]
[InlineData("Waiting for Customer")]
[InlineData("Resolved")]
public async Task AC2183_WaitingOrResolvedTicket_DoesNotEscalate(string status)
{
    var ticketId = await CreateTicketAsync();
    await BackdateAsync(ticketId, resolution: false);
    await MoveToStatusAsync(ticketId, status);
    (await ScanAsync()).Should().Be(0);
    var after = await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
    after!.Data!.EscalationState.Should().Be("None");
    (await EscalatedHistoryCountAsync(ticketId)).Should().Be(0);
}

/// <summary>
/// Walks the lifecycle to <paramref name="target"/> through valid transitions, assigning when a
/// work state is in the path (AC-505).
/// </summary>
private async Task MoveToStatusAsync(Guid ticketId, string target)
{
    if (target is "Waiting for Customer" or "Waiting for Internal Team")
    {
        // Need: Open → Assigned → In Progress → target  (assign first)
        var assignRv = (await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}")).!.Data!.RowVersion;
        await _admin.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
            new { assigneeId = _agentId, rowVersion = assignRv });

        foreach (var step in new[] { "Open", "Assigned", "In Progress", target })
        {
            var rv = (await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}")).!.Data!.RowVersion;
            var r = await _admin.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
                new { status = step, rowVersion = rv });
            r.EnsureSuccessStatusCode();
        }
    }
    else
    {
        foreach (var step in new[] { "Open", target })
        {
            var rv = (await _admin.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}")).!.Data!.RowVersion;
            var r = await _admin.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
                new { status = step, rowVersion = rv });
            r.EnsureSuccessStatusCode();
        }
    }
}
```

**Step 6 — `SlaTrackingEndpointTests` AC133 direct-write update.**

Edit `SlaTrackingEndpointTests.cs:310`: `"Pending"` → `"Waiting for Customer"`. Update the test name and comment from "Pending" to "WaitingForCustomer".

**Tests this task writes:**

| Test | AC | Level |
|---|---|---|
| `Ticket_SlaPause_WaitingForCustomer_ShiftsDueDates` | AC-504 | Unit |
| `Ticket_SlaPause_WaitingForInternalTeam_ShiftsDueDates` | AC-504 | Unit |
| `SlaPause_AccumulatesAcrossCycles` (2 cycles) | AC-504 | Unit |
| `AC134_TransitionToWaitingForCustomer_SetsPausedAt` | AC-134 | Integration |
| `AC135_ExitingWaitingForCustomer_AccumulatesPausedSecondsAndShiftsDueDates` | AC-135 | Integration |
| `AC136_MultipleWaitingCycles_AccumulatePausedSeconds` | AC-136 | Integration |
| `AC2183_WaitingOrResolvedTicket_DoesNotEscalate` | AC-218.3 | Integration |
| `AC133_WaitingForCustomerTicket_IsNotEvaluated` | AC-133 | Integration |

---

### Task 8: Self-assign (`US-903` AC2; `AC-533` backend) — assign endpoint authorization

**Files:**
- `backend/src/CustomerSupport.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommand.cs` — update comment
- `backend/src/CustomerSupport.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommandHandler.cs` — self/role guard
- `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` — endpoint policy + doc update
- `backend/tests/CustomerSupport.Tests/Integration/TicketLifecycleEndpointTests.cs` — new tests

**Step 1 — the failing integration tests.**

Add to `TicketLifecycleEndpointTests.cs`:

```csharp
[Fact]
[Trait("AC", "533")]
public async Task AC533_Agent_SelfAssign_FromQueue_Returns200()
{
    var id = await CreateTicketAsync();

    // Agent assigns the ticket to themselves — this is the self-assign per AC-533
    var response = await AssignAsync(_agent, id, _agentUser.Id);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    (await DetailAsync(_supervisor, id)).AssigneeId.Should().Be(_agentUser.Id);
}

[Fact]
[Trait("AC", "533")]
public async Task AC533_Agent_AssigningAnotherAgent_Returns403()
{
    var id = await CreateTicketAsync();

    var response = await AssignAsync(_agent, id, _otherAgentUser.Id);

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

**Step 2 — the handler self/role guard.**

Edit `AssignTicketCommandHandler.cs`: after the `target.IsActive` check and before `ticket.AssignTo(...)`, add:

```csharp
var isSupervisor = userContext.HasAnyRole(ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin);
if (!isSupervisor && request.AssigneeId != userContext.UserId)
{
    return messages.Fail<Guid>(ApplicationErrors.Ticket.ASSIGNMENT_REFUSED, MessageType.Forbidden);
}
```

This uses `MessageType.Forbidden` — the same pattern as `ChangeTicketStatusCommandHandler.cs:33`
(`NOT_ASSIGNED_TO_YOU`). Add `ASSIGNMENT_REFUSED = "ASSIGNMENT_REFUSED"` to `ApplicationErrors.Ticket`
and map it in `SystemCodeMap` + `SystemCode` + `Resources.yaml`.

**Step 3 — `AssignTicketCommand` comment update.**

Edit `AssignTicketCommand.cs:5-11`: remove "AC-43 is not enforced here" and update the XML doc:

```csharp
/// <summary>
/// Assigns or reassigns a ticket — AC-42, AC-44, BASE-13, AC-533.
///
/// AC-43 (agents may not assign at all) and AC-533 (self-assign allowed) are both enforced
/// in the handler via <see cref="IUserContext"/>, so the endpoint policy is relaxed to
/// Authenticated and the handler makes the per-call decision.
/// </summary>
```

**Step 4 — `TicketsController` endpoint policy + doc update.**

Edit `TicketsController.cs:187-188`: change `[Authorize(Policy = "Supervisor")]` to `[Authorize(Policy = "Authenticated")]`.

Update the XML doc comment at lines 175-183: replace "Supervisors only (AC-43)" with "Any authenticated user may assign; agents may only assign to themselves (AC-533)".

**Tests this task writes:**

| Test | AC | Level |
|---|---|---|
| `AC533_Agent_SelfAssign_FromQueue_Returns200` | AC-533 | Integration |
| `AC533_Agent_AssigningAnotherAgent_Returns403` | AC-533 | Integration |

---

### Task 9: Shared frontend status model (`US-919`; `AC-532`)

**Files:**
- `frontend/projects/common/src/lib/tickets/status.model.ts` — new
- `frontend/projects/common/src/lib/tickets/ticket.api.ts` — `TICKET_STATUSES` + `PERMITTED_TRANSITIONS` update
- `frontend/projects/common/src/lib/ui/badge.component.ts` — `STATUS_TONE` update (8 + escalated)
- `frontend/projects/common/src/lib/ui/status-pill.component.ts` — `STATUS_TINT` + `STATUS_DOT` update
- `frontend/projects/common/src/lib/styles/theme.css` — status color CSS vars (find the correct path first)
- `frontend/projects/admin-app/src/app/features/dashboard/dashboard.component.ts` — status colour mapping
- `frontend/projects/common/src/lib/tickets/ticket.api.spec.ts` — spec updates
- `frontend/projects/common/src/lib/ui/status-pill.component.spec.ts` — spec updates
- `frontend/projects/common/src/lib/ui/badge.component.spec.ts` — spec updates

**Step 1 — the new shared status model.**

Create `frontend/projects/common/src/lib/tickets/status.model.ts`:

```typescript
/**
 * The eight ticket lifecycle states — exactly matching the backend `TicketStatus` value object
 * (AC-501, AC-532). No server authority is assumed here; the server is always the source of
 * truth for what transitions are currently permitted.
 */
export type TicketStatusValue =
  | 'New'
  | 'Open'
  | 'Assigned'
  | 'In Progress'
  | 'Waiting for Customer'
  | 'Waiting for Internal Team'
  | 'Resolved'
  | 'Closed';

/** The eight statuses in lifecycle order. */
export const TICKET_STATUS_VALUES: readonly TicketStatusValue[] = [
  'New',
  'Open',
  'Assigned',
  'In Progress',
  'Waiting for Customer',
  'Waiting for Internal Team',
  'Resolved',
  'Closed',
];

/**
 * Tailwind classes for a solid-fill status badge (used in headers and dense chips).
 * Matches `badge.component.ts` `STATUS_TONE`.  Every key is a literal; Tailwind scans
 * source text for class names so runtime assembly would drop styles in production builds.
 */
export const STATUS_TONE: Readonly<Record<TicketStatusValue, string>> = {
  new: 'bg-status-new text-on-primary',
  open: 'bg-status-open text-on-primary',
  assigned: 'bg-status-assigned text-on-primary',
  'in progress': 'bg-status-in-progress text-on-primary',
  'waiting for customer': 'bg-status-waiting-for-customer text-on-primary',
  'waiting for internal team': 'bg-status-waiting-for-internal-team text-on-primary',
  resolved: 'bg-status-resolved text-on-primary',
  closed: 'bg-status-closed text-on-primary',
  escalated: 'bg-status-escalated text-on-primary',
};

/**
 * Tailwind classes for a tinted-outlined status pill (used in table rows beside priority pills).
 * Matches `status-pill.component.ts` `STATUS_TINT` + `STATUS_DOT`.
 */
export const STATUS_TINT: Readonly<Record<TicketStatusValue, string>> = {
  new: 'bg-status-new/10 text-status-new border border-status-new/20',
  open: 'bg-status-open/10 text-status-open border border-status-open/20',
  assigned: 'bg-status-assigned/10 text-status-assigned border border-status-assigned/20',
  'in progress': 'bg-status-in-progress/10 text-status-in-progress border border-status-in-progress/20',
  'waiting for customer': 'bg-status-waiting-for-customer/10 text-status-waiting-for-customer border border-status-waiting-for-customer/20',
  'waiting for internal team': 'bg-status-waiting-for-internal-team/10 text-status-waiting-for-internal-team border border-status-waiting-for-internal-team/20',
  resolved: 'bg-status-resolved/10 text-status-resolved border border-status-resolved/20',
  closed: 'bg-status-closed/10 text-status-closed border border-status-closed/20',
  escalated: 'bg-status-escalated/10 text-status-escalated border border-status-escalated/20',
};

export const STATUS_DOT: Readonly<Record<TicketStatusValue, string>> = {
  new: 'bg-status-new',
  open: 'bg-status-open',
  assigned: 'bg-status-assigned',
  'in progress': 'bg-status-in-progress',
  'waiting for customer': 'bg-status-waiting-for-customer',
  'waiting for internal team': 'bg-status-waiting-for-internal-team',
  resolved: 'bg-status-resolved',
  closed: 'bg-status-closed',
  escalated: 'bg-status-escalated',
};

/**
 * The server's transition table (AC-501), mirrored so the UI can grey out unavailable actions
 * without a round-trip.  The server remains the authority; a drifted client still gets 409.
 */
export const PERMITTED_TRANSITIONS: Readonly<Record<TicketStatusValue, readonly TicketStatusValue[]>> = {
  New: ['Open'],
  Open: ['Assigned', 'Resolved'],
  Assigned: ['In Progress'],
  'In Progress': ['Waiting for Customer', 'Waiting for Internal Team', 'Resolved'],
  'Waiting for Customer': ['In Progress'],
  'Waiting for Internal Team': ['In Progress'],
  Resolved: ['In Progress', 'Closed'],
  Closed: ['In Progress'],
};
```

**Step 2 — `ticket.api.ts` update.**

Edit `ticket.api.ts:10-12` (`TICKET_STATUSES`) and lines 140-146 (`PERMITTED_TRANSITIONS`):

```typescript
// Before:
export const TICKET_STATUSES = ['New', 'Open', 'Pending', 'Resolved', 'Closed'] as const;
export type TicketStatus = (typeof TICKET_STATUSES)[number];

// After:
export const TICKET_STATUSES = ['New', 'Open', 'Assigned', 'In Progress', 'Waiting for Customer', 'Waiting for Internal Team', 'Resolved', 'Closed'] as const;
export type TicketStatus = (typeof TICKET_STATUSES)[number];

// PERMITTED_TRANSITIONS — replace with the 8-state table from status.model.ts
export const PERMITTED_TRANSITIONS: Readonly<Record<TicketStatus, readonly TicketStatus[]>> = {
  New: ['Open'],
  Open: ['Assigned', 'Resolved'],
  Assigned: ['In Progress'],
  'In Progress': ['Waiting for Customer', 'Waiting for Internal Team', 'Resolved'],
  'Waiting for Customer': ['In Progress'],
  'Waiting for Internal Team': ['In Progress'],
  Resolved: ['In Progress', 'Closed'],
  Closed: ['In Progress'],
};
```

**Step 3 — `badge.component.ts` `STATUS_TONE` update.**

Replace `badge.component.ts:30-37` (`STATUS_TONE` record) with the 8-state entries plus escalated:

```typescript
const STATUS_TONE: Readonly<Record<string, string>> = {
  new: 'bg-status-new text-on-primary',
  open: 'bg-status-open text-on-primary',
  assigned: 'bg-status-assigned text-on-primary',
  'in progress': 'bg-status-in-progress text-on-primary',
  'waiting for customer': 'bg-status-waiting-for-customer text-on-primary',
  'waiting for internal team': 'bg-status-waiting-for-internal-team text-on-primary',
  resolved: 'bg-status-resolved text-on-primary',
  closed: 'bg-status-closed text-on-primary',
  escalated: 'bg-status-escalated text-on-primary',
};
```

**Step 4 — `status-pill.component.ts` `STATUS_TINT` and `STATUS_DOT` update.**

Replace the two records with the 8-state entries:

```typescript
const STATUS_TINT: Readonly<Record<string, string>> = {
  new: 'bg-status-new/10 text-status-new border border-status-new/20',
  open: 'bg-status-open/10 text-status-open border border-status-open/20',
  assigned: 'bg-status-assigned/10 text-status-assigned border border-status-assigned/20',
  'in progress': 'bg-status-in-progress/10 text-status-in-progress border border-status-in-progress/20',
  'waiting for customer': 'bg-status-waiting-for-customer/10 text-status-waiting-for-customer border border-status-waiting-for-customer/20',
  'waiting for internal team': 'bg-status-waiting-for-internal-team/10 text-status-waiting-for-internal-team border border-status-waiting-for-internal-team/20',
  resolved: 'bg-status-resolved/10 text-status-resolved border border-status-resolved/20',
  closed: 'bg-status-closed/10 text-status-closed border border-status-closed/20',
  escalated: 'bg-status-escalated/10 text-status-escalated border border-status-escalated/20',
};

const STATUS_DOT: Readonly<Record<string, string>> = {
  new: 'bg-status-new',
  open: 'bg-status-open',
  assigned: 'bg-status-assigned',
  'in progress': 'bg-status-in-progress',
  'waiting for customer': 'bg-status-waiting-for-customer',
  'waiting for internal team': 'bg-status-waiting-for-internal-team',
  resolved: 'bg-status-resolved',
  closed: 'bg-status-closed',
  escalated: 'bg-status-escalated',
};
```

**Step 5 — `theme.css` color vars.**  First find the correct path:

```powershell
Get-ChildItem -LiteralPath "frontend/projects/common/src/lib" -Recurse -Name "theme.css"
```

Then add the new CSS custom properties (the old `--color-status-pending` can be remapped to the new value or deprecated):

```css
/* Status colours — 8-state machine (FEAT-28, AC-501). */
:root {
  --color-status-new: #3b82f6;
  --color-status-open: #06b6d4;
  --color-status-assigned: #8b5cf6;
  --color-status-in-progress: #f59e0b;
  --color-status-waiting-for-customer: #ef4444;
  --color-status-waiting-for-internal-team: #f97316;
  --color-status-resolved: #22c55e;
  --color-status-closed: #6b7280;
  --color-status-escalated: #dc2626;
}
```

**Step 6 — admin-app dashboard component.** Update the status-to-colour mapping at the location identified in the file (cite lines 136-138 from the grep). Replace any hardcoded colour logic with references to `STATUS_TONE` from the shared model.

**Step 7 — spec rewrites.** Update `ticket.api.spec.ts`, `status-pill.component.spec.ts`, and `badge.component.spec.ts` to assert the new 8-state entries and that `PERMITTED_TRANSITIONS` has exactly 12 legal pairs.

**Tests this task writes:**

| Test | AC | Level |
|---|---|---|
| `StatusModel_ContainsExactlyEightStatuses` | AC-532 | Unit |
| `StatusModel_PERMITTED_TRANSITIONS_Has12LegalPairs` | AC-532 | Unit |
| `BadgeComponent_RendersAllEightStatusTones` | AC-532 | Component |
| `StatusPillComponent_RendersAllEightStatusTintsAndDots` | AC-532 | Component |
| `TicketApi_PermittedTransitions_MatchesBackendTable` | AC-532 | Integration |

---

## Appendix: Test Rework Across the Suite

The following test files require line-level edits to replace `Pending`-era logic with the 8-state machine.

### `TicketLifecycleEndpointTests.cs`

| Line(s) | Change |
|---|---|
| 106–125 `TicketAtAsync` | Replace entire method — assign before work states, new status paths (see Task 6 Step 2). |
| 113 `"Pending" => ["Open", "Pending"]` | Remove; replaced by `TicketAtAsync` switch. |
| 151–158 `AC37` InlineData | Replace the 5 old pairs with 8 new ones: `("New","Open")`, `("Open","Assigned")`, `("Open","Resolved")`, `("Assigned","In Progress")`, `("In Progress","Waiting for Customer")`, `("Waiting for Customer","In Progress")`, `("In Progress","Resolved")`, `("Resolved","Closed")`. |
| 175–177 `AC38` comment | Update to reflect new 8 states (no logic change — New→Closed, Closed→Resolved, New→Resolved remain illegal). |
| 250–264 `AC40_Reopen` | Change `ChangeStatusAsync(_supervisor, id, "Open")` → `"In Progress"`; add assignment before the reopen (assign ticket to _agentUser.Id first). |
| 262 `detail.Status.Should().Be("Open")` | Change to `"In Progress"`. |
| 263 `detail.History[0].ToValue.Should().Be("Open")` | Change to `"In Progress"`. |
| 284 `ChangeStatusAsync(_supervisor, id, "Pending", shared)` | Replace `"Pending"` with `"Assigned"` (legal from Open; still tests concurrency conflict). |
| 595–618 `AC48` walk | Add assignment after creation; change final `ChangeStatusAsync(_supervisor, id, "Open")` → `"In Progress"`. |
| 617 `reopened.ToValue.Should().Be("Open")` | Change to `"In Progress"`. |

### `SlaPauseAndEscalationEndpointTests.cs`

| Line(s) | Change |
|---|---|
| 20–22 `_admin` field + `_agentId` | Add `_agentId` field; in `InitializeAsync` create an agent client and store its user Id. |
| 66–71 `ChangeStatusAsync` | Keep as-is (already reads fresh RowVersion each call). |
| 73 `AssignAsync` helper | Add new private method: `await _admin.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee\", new { assigneeId = _agentId, rowVersion = await RowVersionAsync(ticketId) }); response.StatusCode.Should().Be(HttpStatusCode.OK);` |
| 77–87 `AC134` | Rewrite: after Open, `AssignAsync` then walk `Assigned → In Progress → Waiting for Customer`. |
| 91–120 `AC135` | Same walk; after entering WaitingForCustomer, delay, then `ChangeStatusAsync(ticketId, "In Progress")`. |
| 124–143 `AC136` | Same walk; two cycles WFC → InProgress. |

### `AutoEscalationEndpointTests.cs`

| Line(s) | Change |
|---|---|
| 207 `[InlineData("Pending")]` | Change to `[InlineData("Waiting for Customer")]`. |
| 208 test name | `PendingOrResolvedTicket` → `WaitingOrResolvedTicket`. |
| 223–225 comment | Update "New→Open→Pending|Resolved" → "New→Open→Assigned→In Progress→Waiting for Customer|Resolved". |
| 227–236 `MoveToStatusAsync` | Replace with the version from Task 7 Step 5 that handles work-state paths with assignment. |

### `SlaTrackingEndpointTests.cs`

| Line(s) | Change |
|---|---|
| 301 test name | `PendingTicket_IsNotEvaluated` → `WaitingForCustomerTicket_IsNotEvaluated`. |
| 310 `"Pending"` | Change to `"Waiting for Customer"`. |

### `TicketStatusTests.cs`

| Line(s) | Change |
|---|---|
| 18–28 (entire file) | Replace with the new `TicketStatusTests.cs` shown in Task 5 Step 1. |

### `TicketTests.cs` (unit)

| Line(s) | Change |
|---|---|
| 36 `TicketAt` helper | Update paths: replace all `"Pending"` entries with work-state walks. See Task 6 Step 1 for the full helper replacement. |

---

## Traceability: AC → Test Name

| AC | Test Name | Level |
|---|---|---|
| AC-501 | `TicketStatus_AllowsEachLegalTransition` (12 pairs) | Unit |
| AC-502 | `TicketStatus_RefusesEveryIllegalTransition` (52 pairs) + `Create_RejectsStatusesOutsideTheEight` | Unit |
| AC-503 | `Ticket_Reopening_RecordsReopenedHistory` | Unit |
| AC-504 | `Ticket_SlaPause_WaitingForCustomer_ShiftsDueDates` + `Ticket_SlaPause_WaitingForInternalTeam_ShiftsDueDates` + `SlaPause_AccumulatesAcrossCycles` | Unit |
| AC-505 | `Ticket_EnteringWorkState_WithoutAssignee_Throws` + `Ticket_EnteringWorkState_WhenAssigned_Proceeds` + `Ticket_WorkStates_RequireAssignee` + `AC505_UnassignedTicket_EnteringWorkState_Returns409` + `AC505_ReopeningUnassignedTicket_Returns409` | Unit + Integration |
| AC-506 | `Ticket_TakeEscalation_SetsOwner_RecordsHistory` | Unit |
| AC-507 | `TicketStatus_All_DoesNotIncludeEscalated` | Unit |
| AC-508 | `TC01_Team_Create_WithValidName` + `TC02_Team_Deactivate_TogglesIsActive` + `AC508_CreateTeam_DuplicateNameInSameDepartment_Returns409` + `AC508_CreateTeam_AdminOnly_Returns403` | Unit + Integration |
| AC-509 | `AC509_Migration_AddsTeamFks_KeepsExistingRows` | Integration |
| AC-510 | `Ticket_RecordResponse_SetsFirstAndLast` + `Ticket_Resolve_Close_StampAndReopenClears` + `FirstOutboundMessage_StampsTicket` + `AC510_ListRows_CarryLifecycleTimestamps` | Unit + Integration |
| AC-511 | `Ticket_Assign_PropagatesOrg` + `CreateTicket_InheritsActingAgentOrg` + `AC511_Assign_InheritsAssigneeOrg` | Unit + Integration |
| AC-512 | `AC512_DefaultOrg_Traversable` | Integration |
| AC-532 | `StatusModel_ContainsExactlyEightStatuses` + `StatusModel_PERMITTED_TRANSITIONS_Has12LegalPairs` + `BadgeComponent_RendersAllEightStatusTones` + `StatusPillComponent_RendersAllEightStatusTintsAndDots` + `TicketApi_PermittedTransitions_MatchesBackendTable` | Unit + Component |
| AC-533 | `AC533_Agent_SelfAssign_FromQueue_Returns200` + `AC533_Agent_AssigningAnotherAgent_Returns403` | Integration |
| AC-134 | `AC134_TransitionToWaitingForCustomer_SetsPausedAt` | Integration |
| AC-135 | `AC135_ExitingWaitingForCustomer_AccumulatesPausedSecondsAndShiftsDueDates` | Integration |
| AC-136 | `AC136_MultipleWaitingCycles_AccumulatePausedSeconds` | Integration |
| AC-133 | `AC133_WaitingForCustomerTicket_IsNotEvaluated` | Integration |
| AC-218.3 | `AC2183_WaitingOrResolvedTicket_DoesNotEscalate` | Integration |
| AC-536 | _(undefined — no test written; see Deviation D3)_ | — |

---

## Planned Deviations from the Spec Text

**D1 — Migration is not data-only (spec claim).**  
The spec says the Status column migration is "data-only". This is wrong: `"Waiting for Customer"` (19 chars) and `"Waiting for Internal Team"` (23 chars) both exceed the current `nvarchar(16)` limit. The `Phase2Workflow` migration must `AlterColumn` the Status column to `nvarchar(32)` before running `UPDATE Tickets SET Status = 'Waiting for Customer' WHERE Status = 'Pending'`. No existing row is deleted; the migration is additive.

**D2 — AC-505's wire answer is 409, not 403.**  
The spec's AC-505 describes a "403-class refusal". The spec's own Error-behaviour section says "no new failure codes". The existing `ChangeTicketStatusCommandHandler` maps `InvalidOperationException` → `MessageType.Conflict` (409). The aggregate guard throws exactly that exception, so the existing 409 is the correct wire answer. The handler needs no change.

**D3 — AC-536 does not exist.**  
The spec's AC numbering runs AC-501…AC-535 in the header; the EPIC-14 and block headers also cite AC-536 (undefined). No test names an AC-536. This gap is recorded here rather than fabricated.

---

## Slice 1 Exit Gates

After completing Tasks 5–9:

1. **Backend:** `cd backend; dotnet build CustomerSupport.slnx` — zero warnings-as-errors.
2. **Backend tests:** `cd backend; dotnet test CustomerSupport.slnx` — every test above passes with its `AC` trait printed.
3. **Frontend build:** `cd frontend; npx ng build admin-app` — clean build.
4. **Frontend tests:** `cd frontend; npx ng test common --watch=false` — all green.
5. **Update `rubric-traceability.md`:** mark the EPIC-14 / AC-501…AC-536 rows as "evidenced by `docs/superpowers/plans/EPIC-05-US-218-feat-28-workflow/implementation-plan.md`".

The four-service health check (InternalApi :5074, ExternalApi :5095, admin-app :4300, portal-app :4201) is verified after a full `dotnet run` of each host — not a plan artifact.