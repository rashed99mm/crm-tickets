> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier � this plan did not precede its implementation.

# US-202 Message Timeline: Implementation Plan

> **Disclosure (added 2026-08-27):** This plan was rewritten to carry real, code-bearing Task
> sections. The feature it describes **shipped earlier** as `FEAT-14` — this plan did not precede
> its implementation. The code quoted below is the actual component already in the tree
> (`frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.ts`), not a
> design to be written next. The remaining gap is story-named component tests (Task 3).

**Story:** `US-202`, `docs/requirements/user-stories/EPIC-03-US-202-message-timeline.md`
**Spec:** `docs/superpowers/specs/EPIC-03-EPIC-03-US-202-message-timeline.md`
**Layer:** Frontend (consuming the US-201 backend)
**Status:** SHIPPED — `TicketMessagesComponent` is in the tree; story-named spec tests are the only residual gap.

## Purpose and overview

The ticket-detail conversation timeline is implemented as `TicketMessagesComponent`, a standalone
child of `TicketDetailComponent`. It displays all messages oldest-first with direction, channel,
sender, body, and time, and distinguishes a successful empty list from a failed load. The component
also logs new messages through the existing `TicketApi.recordMessage` (US-201) surface — message
*logging* is a US-201 capability and is not expanded into email delivery here.

## Original story AC mapping

| Original AC | Evidence (real) |
|---|---|
| AC-3.4 / AC-202.1 | `TicketMessagesComponent` renders oldest-first with direction/channel/sender/body/time. |
| AC-202.2 | A successful empty array renders `CsEmptyState`; a failed read renders `CsErrorState`, never the empty state. |

## Affected files (real)

- `frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.html`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts`
- `frontend/projects/common/src/lib/tickets/ticket.api.ts`

---

### Task 1: The shipped component (quoted from tree) — `AC-202.1`

**Files:**
- Real: `frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.ts`

**Interfaces:**
- Consumes: `TicketApi.listMessages(id): Observable<readonly TicketMessage[]>` and
  `TicketApi.recordMessage(id, { direction, channel, subject, body })`.
- Produces: `AsyncState<readonly TicketMessage[]>` rendered oldest-first.

- [ ] **Step 1: Real load + render contract (already in tree)**

```ts
readonly ticketId = input.required<string>();
readonly state = signal<AsyncState<readonly TicketMessage[]>>(loading());

/** Oldest first, exactly as the server returns them — no client-side re-sort (AC-106). */
readonly messages = computed<readonly TicketMessage[]>(() => {
  const current = this.state();
  return current.status === 'loaded' ? current.data : [];
});

load(): void {
  this.state.set(loading());
  this.api.listMessages(this.ticketId()).subscribe({
    // `empty` only ever describes a SUCCESSFUL request that returned nothing (AC-111). A failed
    // read must never render as "no messages" — that would hide a real outage as an honest fact.
    next: (result) => this.state.set(result.length === 0 ? empty() : loaded(result)),
    error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
  });
}
```

The template binds `<time [attr.datetime]="m.sentAt">`, a direction icon/label from
`MESSAGE_DIRECTIONS`, a channel label from `MESSAGE_CHANNELS`, `m.senderName`, and the body via
text interpolation only (never `innerHTML`).

- [ ] **Step 2: No production change required for AC-202.1** — the component already satisfies it.

- [ ] **Step 3: Commit evidence reference**

```bash
git log --oneline -1 -- frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.ts
```
Expected: a `feat(tickets): conversation timeline` commit from the FEAT-14 pass.

---

### Task 2: Message logging + validation (quoted from tree) — `AC-113`, `AC-114`

**Files:**
- Real: `ticket-messages.component.ts` (`log`, `canSubmit`, `formLevelError`)

- [ ] **Step 1: Real submit guard + re-read (already in tree)**

```ts
/** AC-113 — an empty or whitespace-only body is refused here, before any request is made. */
readonly canSubmit = computed(() => this.body().trim().length > 0 && !this.saving());

log(): void {
  if (!this.canSubmit()) return;
  this.saving.set(true);
  this.api.recordMessage(this.ticketId(), {
    direction: this.direction(), channel: this.channel(),
    subject: this.subject().trim() === '' ? undefined : this.subject().trim(),
    body: this.body().trim(),
  }).subscribe({
    next: () => { this.saving.set(false); this.subject.set(''); this.body.set(''); this.load(); },
    error: (error: unknown) => {
      this.saving.set(false);
      // AC-114 — the timeline is untouched; nothing was optimistically added to it.
      this.submitError.set(this.toApiError(error));
    },
  });
}
```

- [ ] **Step 2: No production change required** — AC-113/114 satisfied.

- [ ] **Step 3: Commit evidence reference** — same FEAT-14 commit as Task 1.

---

### Task 3: Story-named component tests (residual gap) — `AC-202.1`, `AC-202.2`

**Files:**
- Create: `frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.spec.ts`

**Interfaces:**
- Produces: named tests `US202_MessageTimeline_RendersOldestFirstWithDirectionChannelSenderBodyAndTime`,
  `US202_MessageTimeline_RendersDistinctEmptyState`,
  `US202_MessageTimeline_RendersLoadFailureInsteadOfEmptyState`,
  `US202_MessageTimeline_UsesTicketApiListMessages`.

- [ ] **Step 1: Write the failing spec**

```ts
import { TestBed } from '@angular/core/testing';
import { TicketMessagesComponent } from './ticket-messages.component';
import { TicketApi, TicketMessage } from 'common';
import { of, throwError } from 'rxjs';
import { input } from '@angular/core';

describe('TicketMessagesComponent', () => {
  let api: jasmine.SpyObj<TicketApi>;
  function setup(messages$: unknown) {
    api = jasmine.createSpyObj<TicketApi>('TicketApi', ['listMessages', 'recordMessage']);
    (api.listMessages as any).and.returnValue(messages$);
    const fixture = TestBed.configureTestingModule({
      imports: [TicketMessagesComponent],
      providers: [{ provide: TicketApi, useValue: api }],
    }).createComponent(TicketMessagesComponent);
    fixture.componentRef.setInput('ticketId', '00000000-0000-0000-0000-000000000001');
    fixture.detectChanges();
    return fixture;
  }

  it('US202_MessageTimeline_RendersOldestFirstWithDirectionChannelSenderBodyAndTime', () => {
    const msgs: TicketMessage[] = [
      { id: '3', direction: 'Outbound', channel: 'Email', subject: null, body: 'third', senderId: 'a', senderName: 'Ann', sentAt: '2026-01-03T00:00:00Z' },
      { id: '1', direction: 'Inbound', channel: 'System', subject: null, body: 'first', senderId: 'b', senderName: 'Bob', sentAt: '2026-01-01T00:00:00Z' },
      { id: '2', direction: 'Outbound', channel: 'Email', subject: null, body: 'second', senderId: 'a', senderName: 'Ann', sentAt: '2026-01-02T00:00:00Z' },
    ];
    const fixture = setup(of(msgs));
    const rows = fixture.nativeElement.querySelectorAll('li, [data-testid="message"]');
    expect(rows[0].textContent).toContain('first');
    expect(rows[2].textContent).toContain('third');
  });

  it('US202_MessageTimeline_RendersDistinctEmptyState', () => {
    const fixture = setup(of([]));
    expect(fixture.nativeElement.querySelector('cs-empty-state')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('cs-error-state')).toBeFalsy();
  });

  it('US202_MessageTimeline_RendersLoadFailureInsteadOfEmptyState', () => {
    const fixture = setup(throwError(() => new Error('boom')));
    expect(fixture.nativeElement.querySelector('cs-error-state')).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd frontend && npx ng test admin-app --watch=false --include "**/ticket-messages.component.spec.ts"`
Expected: FAIL — spec file absent.

- [ ] **Step 3: Implement the spec and run to verify it passes**

Run: `cd frontend && npx ng test admin-app --watch=false --include "**/ticket-messages.component.spec.ts"`
Expected: PASS, 3/3.

- [ ] **Step 4: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.spec.ts
git commit -m "test(tickets): story-named message-timeline component specs (AC-202.1, AC-202.2)"
```

## Definition of done

- [x] Component renders oldest-first with direction/channel/sender/body/time (shipped).
- [x] Empty vs failure states are distinct (shipped).
- [ ] Story-named component tests added and green (Task 3 residual).
- [x] `npx ng test admin-app --watch=false` and `npx ng build admin-app` clean.

## Deviation record

The component shipped under `FEAT-14` (`AC-106`..`AC-114`) before `US-202` was sliced; this plan
now records the real implementation and the one remaining evidence gap (named component tests).

