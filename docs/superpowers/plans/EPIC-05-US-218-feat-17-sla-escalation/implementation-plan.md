# FEAT-17 SLA Escalation — Frontend Addendum Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the ticket detail screen a live SLA countdown, the ticket queue an escalation badge
and escalation-first sort, and the SLA policies screen an edit path — closing `US-222`, `US-224`
and the `US-223` edit gap.

**Architecture:** One small, additive backend change (`EscalationState` projected onto the queue
row DTO) plus four Angular pieces: a shared `SlaCountdown` component, edits to two existing
components (`ticket-detail`, `ticket-queue`), and an edit form added to a third
(`sla-policies`). No new routes, no new backend endpoints.

**Tech Stack:** .NET 10 / EF Core (backend field), Angular 20 standalone components + signals,
xUnit + `CrmApiFactory` (backend test), Karma/Vitest component tests (frontend).

**Spec:** [`docs/superpowers/specs/EPIC-05-US-218-sla-escalation.md`](../../specs/EPIC-05-US-218-sla-escalation.md)
— addendum section at the end of that file (`AC-155`–`AC-159`, assumptions A5–A8).

## Global Constraints

- Every failing test runs before its implementation, per this project's TDD rule. Paste the actual
  command output when marking a step done — never "should pass."
- New backend work runs targeted (`dotnet test --filter`), not the full suite, per the standing
  "skip backend test" instruction for this session — the full suite was last confirmed green
  (363/364, the one failure external to this project's own work) and does not need re-running for
  a projection-field addition.
- Any new UI text goes through `TRANSLATIONS` (`frontend/projects/common/src/lib/i18n/translations.ts`)
  and the `| t` pipe or `LocaleStore.t(...)` — never a bare string literal in a template, or
  `no-hardcoded-strings.spec.ts` fails the build.
- New Tailwind color tokens go in `frontend/projects/common/src/styles/theme.css`, following its
  existing `@theme` block and comment style — not invented inline as arbitrary hex values.

---

### Task 1: Backend — expose `EscalationState` on the ticket queue row

**Files:**
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Dtos/TicketDtos.cs`
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQueryHandler.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

**Interfaces:**
- Produces: `TicketListItemDto` gains a 12th positional parameter, `string EscalationState`
  (already exists on `Ticket` and on `TicketDetailDto`; this is a projection addition only).

- [ ] **Step 1: Write the failing test**

In `TicketEndpointTests.cs`, add the `EscalationState` field to the local `TicketListRow` record
(line 371) and a new test near `AC32_GetTickets_ReturnsPagedNewestFirst`:

```csharp
public sealed record TicketListRow(
    Guid Id, string Reference, string Subject, string Status, string Priority, string EscalationState);
```

```csharp
[Fact]
[Trait("AC", "158")]
public async Task AC158_GetTickets_ExposesEscalationState()
{
    var ticketId = await CreateTicketAsync("Escalation projection fixture");

    var page = await _client.GetFromJsonAsync<Response<PagedData<TicketListRow>>>(
        $"/api/Tickets?page=1&pageSize=50&customerId={_customerId}");

    var row = page!.Data!.Items.Single(t => t.Id == ticketId);
    row.EscalationState.Should().Be("None");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC158_GetTickets_ExposesEscalationState"`
Expected: FAIL — `System.Text.Json.JsonException` or a compile error, since `TicketListItemDto` has
no `EscalationState` property yet for the response to deserialize into the new record field (the
JSON deserializer will leave it as `null`/default, which fails the `Should().Be("None")` assertion
rather than the JSON reader itself; either way it's red).

- [ ] **Step 3: Add `EscalationState` to `TicketListItemDto`**

In `TicketDtos.cs`, change:

```csharp
public record TicketListItemDto(
    Guid Id,
    string Reference,
    string Subject,
    string Status,
    string Priority,
    Guid CustomerId,
    string CustomerName,
    Guid CategoryId,
    string CategoryName,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTime CreatedAt);
```

to:

```csharp
public record TicketListItemDto(
    Guid Id,
    string Reference,
    string Subject,
    string Status,
    string Priority,
    Guid CustomerId,
    string CustomerName,
    Guid CategoryId,
    string CategoryName,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTime CreatedAt,
    // FEAT-17 second slice addendum (2026-08-27), AC-158. None/Warning/Level1/Level2/Level3 (BR-32).
    string EscalationState);
```

- [ ] **Step 4: Wire it through the handler's projection**

In `GetTicketsQueryHandler.cs`, change the projection (line 40):

```csharp
var ticketItems = await tickets.ListProjectedOrderedAsync(
    filter,
    t => new { t.Id, t.Reference, t.Subject, t.Status, t.Priority, t.CustomerId, t.CategoryId, t.AssigneeId, t.CreatedAt },
    t => t.CreatedAt,
    descending: true,
    ct);
```

to:

```csharp
var ticketItems = await tickets.ListProjectedOrderedAsync(
    filter,
    t => new { t.Id, t.Reference, t.Subject, t.Status, t.Priority, t.CustomerId, t.CategoryId, t.AssigneeId, t.CreatedAt, t.EscalationState },
    t => t.CreatedAt,
    descending: true,
    ct);
```

and the `TicketListItemDto` construction (line 67):

```csharp
var items = pagedTickets.Select(t => new TicketListItemDto(
    t.Id,
    t.Reference,
    t.Subject,
    t.Status,
    t.Priority,
    t.CustomerId,
    customerMap.TryGetValue(t.CustomerId, out var cust) ? cust.Name : string.Empty,
    t.CategoryId,
    categoryMap.TryGetValue(t.CategoryId, out var cat) ? cat.Name : string.Empty,
    t.AssigneeId,
    t.AssigneeId.HasValue ? assigneeMap.GetValueOrDefault(t.AssigneeId.Value, string.Empty) : null,
    t.CreatedAt,
    t.EscalationState)).ToList();
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC158_GetTickets_ExposesEscalationState|FullyQualifiedName~AC32_GetTickets_ReturnsPagedNewestFirst"`
Expected: PASS, both tests (the second guards against breaking the existing shape).

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Tickets/Dtos/TicketDtos.cs backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQueryHandler.cs backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs
git commit -m "feat(tickets): expose EscalationState on the queue row (AC-158)"
```

---

### Task 2: Frontend — add SLA countdown color tokens and dictionary entries

**Files:**
- Modify: `frontend/projects/common/src/styles/theme.css`
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts`

**Interfaces:**
- Produces: Tailwind utility classes `text-warning`, `bg-warning` (new token); `text-status-escalated`,
  `bg-status-escalated` (existing token, first real use); dictionary keys consumed by Tasks 3–5.

- [ ] **Step 1: Add the warning token**

In `theme.css`, after the `--color-error*` group (after line 58, before the ticket-status group):

```css
  /* ── SLA countdown urgency (S2 addendum, US-222/AC-156).
     Amber matches the meaning --color-status-pending/--color-priority-high already carry.
     Danger reuses --color-error rather than a third red — an overdue SLA is exactly that. ── */
  --color-warning: #f59e0b;
```

(`--color-status-escalated` already exists, reserved exactly for this slice per its own comment —
used unmodified by Task 4's badge.)

- [ ] **Step 2: Add dictionary entries**

In `translations.ts`, add near the existing `tickets.*` and `slaPolicies.*` groups:

```ts
  'sla.countdown.left': { en: '{0} left', ar: 'متبقي {0}' },
  'sla.countdown.overdue': { en: '{0} overdue', ar: 'متأخر {0}' },
  'tickets.detail.responseDue': { en: 'Response due', ar: 'موعد الاستجابة' },
  'tickets.detail.resolutionDue': { en: 'Resolution due', ar: 'موعد الحل' },
  'tickets.escalation.level1': { en: 'Level 1', ar: 'المستوى 1' },
  'tickets.escalation.level2': { en: 'Level 2', ar: 'المستوى 2' },
  'tickets.escalation.level3': { en: 'Level 3', ar: 'المستوى 3' },
  'tickets.queue.sortByEscalation': { en: 'Sort by escalation', ar: 'الفرز حسب التصعيد' },
  'slaPolicies.edit': { en: 'Edit', ar: 'تعديل' },
  'slaPolicies.edit.title': { en: 'Edit policy', ar: 'تعديل السياسة' },
  'slaPolicies.edit.submit': { en: 'Save changes', ar: 'حفظ التغييرات' },
  'slaPolicies.edit.cancel': { en: 'Cancel', ar: 'إلغاء' },
```

- [ ] **Step 3: No test for this step in isolation** — both are consumed and proven by Tasks 3–5's
component tests, which would fail to compile/render correctly without these keys existing (a
missing `TranslationKey` is a TypeScript compile error, since `TranslatePipe` is typed against
`keyof typeof TRANSLATIONS`).

- [ ] **Step 4: Commit**

```bash
git add frontend/projects/common/src/styles/theme.css frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat(common): add SLA countdown/escalation tokens and dictionary entries"
```

---

### Task 3: Frontend — `SlaCountdown` shared component + ticket detail wiring (AC-155, AC-156)

**Files:**
- Create: `frontend/projects/common/src/lib/tickets/sla-countdown.component.ts`
- Create: `frontend/projects/common/src/lib/tickets/sla-countdown.component.html`
- Create: `frontend/projects/common/src/lib/tickets/sla-countdown.component.spec.ts`
- Modify: `frontend/projects/common/src/public-api.ts`
- Modify: `frontend/projects/common/src/lib/tickets/ticket.api.ts`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html`
- Test: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.spec.ts`
  (create if it does not already exist — confirm with `ls` before assuming; if absent, this task
  creates a minimal one covering only the two new assertions below, not a full retrofit of the
  component's existing behaviour)

**Interfaces:**
- Produces: `SlaCountdown` (selector `cs-sla-countdown`), inputs `dueAt: string | null`,
  `createdAt: string`, `label: string`. Exported from `common`'s `public-api.ts`.
- Consumes: `TicketDetail` gains `responseDueAt: string | null`, `resolutionDueAt: string | null`,
  `escalationState: string` (matching the backend's `TicketDetailDto`, already shipped — this is a
  client-side type catch-up, not a backend change).

- [ ] **Step 1: Write the failing component test**

```ts
// frontend/projects/common/src/lib/tickets/sla-countdown.component.spec.ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SlaCountdown } from './sla-countdown.component';

describe('SlaCountdown', () => {
  function render(dueAt: string | null, createdAt: string): ComponentFixture<SlaCountdown> {
    const fixture = TestBed.createComponent(SlaCountdown);
    fixture.componentRef.setInput('dueAt', dueAt);
    fixture.componentRef.setInput('createdAt', createdAt);
    fixture.componentRef.setInput('label', 'Response due');
    fixture.detectChanges();
    return fixture;
  }

  it('AC155: renders nothing when there is no due date', () => {
    const fixture = render(null, '2026-08-27T00:00:00Z');
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');
  });

  it('AC156: renders the danger style once the due date has passed', () => {
    const past = new Date(Date.now() - 60_000).toISOString();
    const created = new Date(Date.now() - 3_600_000).toISOString();
    const fixture = render(past, created);

    const el = (fixture.nativeElement as HTMLElement).querySelector('[data-urgency]');
    expect(el?.getAttribute('data-urgency')).toBe('danger');
  });

  it('AC156: renders the warning style once under 20% of the window remains', () => {
    const created = new Date(Date.now() - 100_000).toISOString();
    // Total window ~110s (created 100s ago, due in 10s more) — remaining 10s is < 20% of 110s.
    const due = new Date(Date.now() + 10_000).toISOString();
    const fixture = render(due, created);

    const el = (fixture.nativeElement as HTMLElement).querySelector('[data-urgency]');
    expect(el?.getAttribute('data-urgency')).toBe('warning');
  });

  it('AC156: renders the normal style with most of the window remaining', () => {
    const created = new Date(Date.now() - 10_000).toISOString();
    const due = new Date(Date.now() + 990_000).toISOString();
    const fixture = render(due, created);

    const el = (fixture.nativeElement as HTMLElement).querySelector('[data-urgency]');
    expect(el?.getAttribute('data-urgency')).toBe('normal');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test common --watch=false --include='**/sla-countdown.component.spec.ts'`
Expected: FAIL — `Cannot find module './sla-countdown.component'`.

- [ ] **Step 3: Implement `SlaCountdown`**

```ts
// frontend/projects/common/src/lib/tickets/sla-countdown.component.ts
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { LocaleStore } from '../i18n/locale.store';

export type SlaCountdownUrgency = 'normal' | 'warning' | 'danger';

/**
 * A live countdown to an SLA due date — US-222, AC-155/AC-156.
 *
 * Warning/danger are derived from the window between `createdAt` and `dueAt` (spec addendum A6):
 * no field anywhere carries an explicit warning threshold, so the whole window is treated as 100%
 * and the countdown crosses into warning once less than 20% of it remains, and danger once the due
 * date has passed.
 */
@Component({
  selector: 'cs-sla-countdown',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sla-countdown.component.html',
})
export class SlaCountdown {
  private readonly locale = inject(LocaleStore);

  readonly dueAt = input<string | null>(null);
  readonly createdAt = input.required<string>();
  readonly label = input.required<string>();

  private readonly now = signal(Date.now());

  constructor() {
    const destroyRef = inject(DestroyRef);
    interval(1000)
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe(() => this.now.set(Date.now()));
  }

  private readonly remainingMs = computed<number | null>(() => {
    const due = this.dueAt();
    return due ? new Date(due).getTime() - this.now() : null;
  });

  readonly urgency = computed<SlaCountdownUrgency>(() => {
    const remaining = this.remainingMs();
    const due = this.dueAt();
    if (remaining === null || !due) {
      return 'normal';
    }
    if (remaining <= 0) {
      return 'danger';
    }

    const totalMs = new Date(due).getTime() - new Date(this.createdAt()).getTime();
    if (totalMs <= 0) {
      return 'danger';
    }

    return remaining / totalMs < 0.2 ? 'warning' : 'normal';
  });

  readonly dotClass = computed(
    () =>
      ({
        normal: 'bg-on-surface-variant',
        warning: 'bg-warning',
        danger: 'bg-error',
      })[this.urgency()],
  );

  readonly textClass = computed(
    () =>
      ({
        normal: 'text-on-surface',
        warning: 'text-warning',
        danger: 'text-error',
      })[this.urgency()],
  );

  readonly remainingLabel = computed(() => {
    const remaining = this.remainingMs();
    if (remaining === null) {
      return '';
    }

    const formatted = this.formatDuration(Math.abs(remaining));
    return remaining <= 0
      ? this.locale.t('sla.countdown.overdue', formatted)
      : this.locale.t('sla.countdown.left', formatted);
  });

  private formatDuration(ms: number): string {
    const totalMinutes = Math.floor(ms / 60_000);
    const days = Math.floor(totalMinutes / 1440);
    const hours = Math.floor((totalMinutes % 1440) / 60);
    const minutes = totalMinutes % 60;
    if (days > 0) {
      return `${days}d ${hours}h`;
    }
    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    }
    return `${minutes}m`;
  }
}
```

```html
<!-- frontend/projects/common/src/lib/tickets/sla-countdown.component.html -->
@if (dueAt(); as due) {
  <div class="flex flex-col gap-1">
    <span class="text-label-md tracking-wider text-on-surface-variant uppercase">{{ label() }}</span>
    <div class="flex items-center gap-1.5" [attr.data-urgency]="urgency()">
      <span class="size-1.5 shrink-0 rounded-full" [class]="dotClass()"></span>
      <time [attr.datetime]="due" class="font-mono text-data-mono" [class]="textClass()">
        {{ remainingLabel() }}
      </time>
    </div>
  </div>
}
```

- [ ] **Step 4: Export it and extend `TicketDetail`**

In `public-api.ts`, add after `export * from './lib/tickets/ticket.api';`:

```ts
export * from './lib/tickets/sla-countdown.component';
```

In `ticket.api.ts`, extend `TicketDetail` (line 110):

```ts
export interface TicketDetail {
  readonly id: string;
  readonly reference: string;
  readonly subject: string;
  readonly description: string;
  readonly status: TicketStatus;
  readonly priority: TicketPriority;
  readonly assigneeId: string | null;
  readonly createdAt: string;
  readonly rowVersion: string;
  readonly customer: CustomerSummary;
  readonly categoryName: string;
  readonly history: readonly TicketHistoryEntry[];
  /** FEAT-17, AC-128/AC-129. Null when no active SLAPolicy matched at creation. */
  readonly responseDueAt: string | null;
  readonly resolutionDueAt: string | null;
  /** FEAT-17 second slice, AC-137/AC-138. None/Warning/Level1/Level2/Level3 (BR-32). */
  readonly escalationState: string;
}
```

- [ ] **Step 5: Run the component test to verify it passes**

Run: `cd frontend && npx ng test common --watch=false --include='**/sla-countdown.component.spec.ts'`
Expected: PASS, 4/4.

- [ ] **Step 6: Wire two `SlaCountdown` instances into the ticket detail meta strip**

In `ticket-detail.component.ts`, add `SlaCountdown` to `imports`:

```ts
import { SlaCountdown } from 'common'; // add to the existing `from 'common'` import list
```

(add `SlaCountdown` to the existing named-import block, and to the `@Component` `imports` array.)

In `ticket-detail.component.html`, add two entries to the `<dl>` meta strip (after the "Opened"
entry, before its closing `</dl>` at line 105):

```html
            @if (t.responseDueAt) {
              <div>
                <cs-sla-countdown
                  [dueAt]="t.responseDueAt"
                  [createdAt]="t.createdAt"
                  [label]="'tickets.detail.responseDue' | t"
                />
              </div>
            }

            @if (t.resolutionDueAt) {
              <div>
                <cs-sla-countdown
                  [dueAt]="t.resolutionDueAt"
                  [createdAt]="t.createdAt"
                  [label]="'tickets.detail.resolutionDue' | t"
                />
              </div>
            }
```

- [ ] **Step 7: Write and run the ticket-detail integration test**

Add to `ticket-detail.component.spec.ts` (create the file with the existing `ticket-queue.component.spec.ts`
pattern — `provideHttpClient(withInterceptors([envelopeInterceptor]))`, `provideHttpClientTesting`,
route param via `provideRouter` + `ActivatedRoute`/`input.required` binding — if it does not already
exist; otherwise add this test alongside whatever is there):

```ts
it('AC155: renders a countdown for a ticket with a response due date', () => {
  const fixture = render('t-1');
  flushDetail(fixture, {
    ...BASE_TICKET,
    responseDueAt: new Date(Date.now() + 3_600_000).toISOString(),
    resolutionDueAt: null,
    escalationState: 'None',
  });

  const el = fixture.nativeElement as HTMLElement;
  expect(el.textContent).toContain('Response due');
  expect(el.querySelector('[data-urgency]')).not.toBeNull();
});
```

(`BASE_TICKET` and `flushDetail`/`render` follow the exact `TICKET`/`flushList`/`render` pattern
already established in `ticket-queue.component.spec.ts`, adapted to `GET /api/Tickets/t-1`.)

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/ticket-detail.component.spec.ts'`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add frontend/projects/common/src/lib/tickets/sla-countdown.component.ts frontend/projects/common/src/lib/tickets/sla-countdown.component.html frontend/projects/common/src/lib/tickets/sla-countdown.component.spec.ts frontend/projects/common/src/public-api.ts frontend/projects/common/src/lib/tickets/ticket.api.ts frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.spec.ts
git commit -m "feat(tickets): SLA countdown on the ticket detail screen (AC-155, AC-156)"
```

---

### Task 4: Frontend — escalation badge + sort-by-escalation on the ticket queue (AC-158, AC-159)

**Files:**
- Modify: `frontend/projects/common/src/lib/tickets/ticket.api.ts`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.ts`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.html`
- Test: `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.spec.ts`

**Interfaces:**
- Consumes: `TicketListItemDto.EscalationState` from Task 1.
- Produces: `TicketQueueComponent.sortByEscalation(): void`, a new public method the component's
  own spec and any future caller can rely on.

- [ ] **Step 1: Write the failing tests**

Add to `ticket-queue.component.spec.ts` (after the existing `TICKET` const, add `escalationState`
to it and a second escalated fixture):

```ts
const TICKET = {
  id: 't-1',
  reference: 'TKT-001001',
  subject: 'Cannot sign in',
  status: 'New',
  priority: 'Normal',
  customerId: 'c-1',
  customerName: 'Layla Haddad',
  categoryId: 'cat-1',
  categoryName: 'Technical',
  assigneeId: null,
  createdAt: '2026-08-26T09:00:00Z',
  escalationState: 'None',
};

const ESCALATED_TICKET = { ...TICKET, id: 't-2', reference: 'TKT-001002', escalationState: 'Level1' };
```

```ts
it('AC158: shows an escalation badge on an escalated row', () => {
  const fixture = render();
  flushList(fixture, page([TICKET, ESCALATED_TICKET]));

  const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
  expect(text).toContain('Level 1');
});

it('AC158: does not show a badge on a non-escalated row', () => {
  const fixture = render();
  flushList(fixture, page([TICKET]));

  expect(
    (fixture.nativeElement as HTMLElement).querySelector('[data-testid="escalation-badge"]'),
  ).toBeNull();
});

it('AC159: sorting by escalation moves escalated rows to the top of the loaded page', () => {
  const fixture = render();
  flushList(fixture, page([TICKET, ESCALATED_TICKET]));

  fixture.componentInstance.sortByEscalation();
  fixture.detectChanges();

  expect(fixture.componentInstance.tickets()[0].id).toBe('t-2');
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/ticket-queue.component.spec.ts'`
Expected: FAIL — `TicketListItem` has no `escalationState` (TS compile error) and
`sortByEscalation` does not exist.

- [ ] **Step 3: Extend `TicketListItem`**

In `ticket.api.ts` (line 15):

```ts
export interface TicketListItem {
  readonly id: string;
  readonly reference: string;
  readonly subject: string;
  readonly status: TicketStatus;
  readonly priority: TicketPriority;
  readonly customerId: string;
  readonly customerName: string;
  readonly categoryId: string;
  readonly categoryName: string;
  readonly assigneeId: string | null;
  readonly createdAt: string;
  /** FEAT-17 second slice addendum, AC-158. None/Warning/Level1/Level2/Level3 (BR-32). */
  readonly escalationState: string;
}
```

- [ ] **Step 4: Implement `sortByEscalation` (A7 — client-side, current page only)**

In `ticket-queue.component.ts`, add a signal holding a client-side re-order and fold it into the
existing `tickets` computed:

```ts
  readonly escalationSort = signal(false);

  readonly tickets = computed<readonly TicketListItem[]>(() => {
    const current = this.state();
    const items = current.status === 'loaded' ? current.data.items : [];
    if (!this.escalationSort()) {
      return items;
    }
    // A7 — client-side re-order of the currently loaded page only; the server always orders by
    // CreatedAt and this does not ask it for a second sort dimension.
    return [...items].sort((a, b) => Number(b.escalationState !== 'None') - Number(a.escalationState !== 'None'));
  });

  sortByEscalation(): void {
    this.escalationSort.set(!this.escalationSort());
  }
```

(Replace the existing `tickets` computed at line 66 with this version — same signature, same
callers, so nothing downstream changes.)

- [ ] **Step 5: Render the badge and the sort toggle**

In `ticket-queue.component.ts`, add `escalationLabel(ticket: TicketListItem)`:

```ts
  escalationLabel(ticket: TicketListItem): string | null {
    switch (ticket.escalationState) {
      case 'Level1':
        return this.locale.t('tickets.escalation.level1');
      case 'Level2':
        return this.locale.t('tickets.escalation.level2');
      case 'Level3':
        return this.locale.t('tickets.escalation.level3');
      default:
        return null;
    }
  }
```

In `ticket-queue.component.html`, add a sort toggle beside the existing "my tickets" checkbox
(after line 47):

```html
      <label class="flex items-center gap-2 text-label-md text-on-surface-variant">
        <input
          type="checkbox"
          [checked]="escalationSort()"
          (change)="sortByEscalation()"
          class="accent-primary"
        />
        {{ 'tickets.queue.sortByEscalation' | t }}
      </label>
```

and a badge in the assignee cell's row (inside the `<td>` at line 135, alongside the existing
assignee markup — add a sibling `<span>` before it):

```html
                  <td class="px-4 py-3">
                    <span class="flex min-w-0 items-center gap-2">
                      @if (escalationLabel(ticket); as level) {
                        <span
                          data-testid="escalation-badge"
                          class="inline-flex shrink-0 items-center gap-1 rounded bg-status-escalated/12 px-2 py-0.5 text-label-md font-semibold text-status-escalated"
                        >
                          {{ level }}
                        </span>
                      }
                      @if (ticket.assigneeId) {
```

(the existing `@if (ticket.assigneeId) { ... } @else { ... }` block continues unchanged after
this — only the new `@if (escalationLabel...)` block is inserted before it.)

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/ticket-queue.component.spec.ts'`
Expected: PASS, all tests including the three new ones.

- [ ] **Step 7: Commit**

```bash
git add frontend/projects/common/src/lib/tickets/ticket.api.ts frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.ts frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.html frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.spec.ts
git commit -m "feat(tickets): escalation badge and sort-by-escalation on the queue (AC-158, AC-159)"
```

---

### Task 5: Frontend — SLA policy edit form (AC-157)

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/organisation/sla-policies.component.ts`
- Modify: `frontend/projects/admin-app/src/app/features/organisation/sla-policies.component.html`
- Test: `frontend/projects/admin-app/src/app/features/organisation/sla-policies.component.spec.ts`
  (create — none exists today per the frontend survey; follow the `ticket-queue.component.spec.ts`
  pattern exactly, scoped to only the behaviour this task adds plus a baseline list-render check)

**Interfaces:**
- Consumes: `SLAPolicyApi.update(id, request)` — already exists, unused until this task.

- [ ] **Step 1: Write the failing test**

```ts
// frontend/projects/admin-app/src/app/features/organisation/sla-policies.component.spec.ts
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { envelopeInterceptor } from 'common';
import SLAPoliciesComponent from './sla-policies.component';

function ok<T>(data: T) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

const POLICY = {
  id: 'p-1',
  priority: 'High',
  responseTargetHours: 2,
  resolutionTargetHours: 8,
  categoryId: null,
  branchId: null,
  isActive: true,
  createdAt: '2026-08-20T00:00:00Z',
};

describe('SLAPoliciesComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  function render(): ComponentFixture<SLAPoliciesComponent> {
    const fixture = TestBed.createComponent(SLAPoliciesComponent);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/SLAPolicies').flush(ok({ items: [POLICY], pageIndex: 1, pageSize: 100, totalCount: 1 }));
    fixture.detectChanges();
    return fixture;
  }

  it('AC157: editing a policy calls PUT with the changed values and refreshes the list', () => {
    const fixture = render();

    fixture.componentInstance.startEdit(POLICY as never);
    fixture.detectChanges();
    fixture.componentInstance.editForm.controls.responseTargetHours.setValue(3);
    fixture.componentInstance.saveEdit();

    const request = http.expectOne((r) => r.url === '/api/SLAPolicies/p-1' && r.method === 'PUT');
    expect(request.request.body).toEqual({
      priority: 'High',
      responseTargetHours: 3,
      resolutionTargetHours: 8,
    });
    request.flush(ok(null));

    http.expectOne((r) => r.url === '/api/SLAPolicies').flush(
      ok({ items: [{ ...POLICY, responseTargetHours: 3 }], pageIndex: 1, pageSize: 100, totalCount: 1 }),
    );
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('3');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/sla-policies.component.spec.ts'`
Expected: FAIL — `startEdit`/`editForm`/`saveEdit` do not exist.

- [ ] **Step 3: Add edit state and methods**

In `sla-policies.component.ts`, add after the existing `form` (create form):

```ts
  readonly editingId = signal<string | null>(null);
  readonly editSaving = signal(false);
  readonly editError = signal<ApiError | null>(null);

  readonly editForm = new FormGroup({
    priority: new FormControl('Normal', { nonNullable: true, validators: [Validators.required] }),
    responseTargetHours: new FormControl(4, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.1)],
    }),
    resolutionTargetHours: new FormControl(24, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.1)],
    }),
  });

  startEdit(policy: SLAPolicy): void {
    this.editingId.set(policy.id);
    this.editError.set(null);
    this.editForm.setValue({
      priority: policy.priority,
      responseTargetHours: policy.responseTargetHours,
      resolutionTargetHours: policy.resolutionTargetHours,
    });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editError.set(null);
  }

  saveEdit(): void {
    const id = this.editingId();
    if (!id || this.editForm.invalid || this.editSaving()) {
      return;
    }

    this.editSaving.set(true);
    this.editError.set(null);

    const { priority, responseTargetHours, resolutionTargetHours } = this.editForm.getRawValue();

    this.api
      .update(id, {
        priority: priority as (typeof TICKET_PRIORITIES)[number],
        responseTargetHours,
        resolutionTargetHours,
      })
      .subscribe({
        next: () => {
          this.editSaving.set(false);
          this.editingId.set(null);
          this.load();
        },
        error: (error: unknown) => {
          this.editSaving.set(false);
          this.editError.set(this.toApiError(error));
        },
      });
  }

  editFieldError(field: string) {
    return this.editError()?.fieldError(field) ?? null;
  }
```

- [ ] **Step 4: Render the edit row**

In `sla-policies.component.html`, replace the actions cell (lines 98–108) to branch on
`editingId()`:

```html
                <td class="px-4 py-3">
                  @if (editingId() === policy.id) {
                    <div class="flex flex-wrap items-center justify-end gap-2">
                      <select
                        [formControl]="editForm.controls.priority"
                        class="h-9 rounded-lg border border-outline-variant bg-surface-lowest px-2 text-body-md"
                      >
                        @for (option of priorities; track option) {
                          <option [value]="option">{{ option }}</option>
                        }
                      </select>
                      <input
                        type="number"
                        [formControl]="editForm.controls.responseTargetHours"
                        class="h-9 w-20 rounded-lg border border-outline-variant bg-surface-lowest px-2 text-body-md"
                      />
                      <input
                        type="number"
                        [formControl]="editForm.controls.resolutionTargetHours"
                        class="h-9 w-20 rounded-lg border border-outline-variant bg-surface-lowest px-2 text-body-md"
                      />
                      <cs-button [busy]="editSaving()" [disabled]="editForm.invalid" (pressed)="saveEdit()">
                        {{ 'slaPolicies.edit.submit' | t }}
                      </cs-button>
                      <cs-button variant="secondary" (pressed)="cancelEdit()">
                        {{ 'slaPolicies.edit.cancel' | t }}
                      </cs-button>
                    </div>
                  } @else {
                    <div class="flex justify-end gap-2">
                      <cs-button variant="secondary" (pressed)="startEdit(policy)">
                        {{ 'slaPolicies.edit' | t }}
                      </cs-button>
                      <cs-button
                        variant="secondary"
                        [disabled]="!policy.isActive"
                        (pressed)="deactivate(policy)"
                      >
                        {{ 'departments.deactivate' | t }}
                      </cs-button>
                    </div>
                  }
                </td>
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/sla-policies.component.spec.ts'`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/organisation/sla-policies.component.ts frontend/projects/admin-app/src/app/features/organisation/sla-policies.component.html frontend/projects/admin-app/src/app/features/organisation/sla-policies.component.spec.ts
git commit -m "feat(sla-policies): edit form, closing the US-223 gap (AC-157)"
```

---

## Verification & gates

- Per task: failing test observed, then green, output pasted into the task record — not assumed.
- `cd frontend && npx ng build admin-app` and `npx ng test common --watch=false` run once at the
  end, output pasted, per this project's "clean build under warnings-as-errors" gate.
- `npx ng test common --watch=false --include='**/no-hardcoded-strings.spec.ts'` re-run explicitly
  after Tasks 3–5, since every one of them touches templates.
- Task record written to `docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-escalation/README.md`
  (append a new section — do not overwrite the existing backend section) once all five tasks are
  green, following this project's task-record convention: evidence, what shipped, deviations, gaps.
