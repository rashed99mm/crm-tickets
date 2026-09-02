# FEAT-32 Ticket Domain Enrichment — Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the admin-app UI for resolution discipline, the impact/urgency priority matrix,
ticket tags, and related/duplicate links — the frontend half of FEAT-32, written after backend
implementation completed per the SDD gate.

**Architecture:** Angular 21 standalone components with signals, no NgModules. Each slice touches
`common`'s `TicketApi` (new request/response shapes, new methods) and one or two admin-app
components (`ticket-create`, `ticket-detail`, `ticket-queue`). No new shared UI components are
introduced — every new element (resolve form, tag chips, links list, matrix preview) is built from
the existing "command-center" tokens already used throughout these three screens (`cs-card`,
`cs-badge`, the `rounded-lg border-outline-variant` input treatment, `text-label-md
text-on-surface-variant` labels, `role="alert"` error paragraphs) so the new UI reads as more of
the same screen, not a bolted-on widget.

**Tech Stack:** Angular 21, RxJS, Reactive Forms, Vitest (`ng test`), the existing
`envelopeInterceptor` + `ApiError` contract test harness (`HttpTestingController`).

**Spec:** `docs/superpowers/specs/EPIC-02-US-922-ticket-domain-enrichment.md` — frontend criteria
AC-922.7, AC-923.7, AC-924.5, AC-925.5 (screen halves already named "the plan decides from the
real controller" in the backend plan; this is that plan).

## Global Constraints

- No new npm dependency. Everything is built from `cs-card`, `cs-badge`, `cs-icon`,
  `cs-input-field`, plain `<select>`/`<input>` styled with the literal Tailwind classes already
  used in `ticket-create.component.html` / `ticket-detail.component.html` — copy the exact class
  strings, do not paraphrase them.
- Every new user-facing string goes through `TranslatePipe` (`| t`) with an `en`/`ar` pair added to
  `frontend/projects/common/src/lib/i18n/translations.ts`. No hardcoded English.
- Every new form control gets a real, unique `id` (AC-418, keyboard-accessible-forms — the existing
  test convention checks `select` elements all carry one).
- Server field errors land on the control named by `field` (AC-60's rule, already implemented in
  `ticket-create.component.ts`'s `fieldError`/`clearServerError` — the same pattern is reused for
  every new server-validated control here, not reinvented).
- `AsyncState`/loading-empty-error conventions are not re-litigated: the two screens already use
  them; new UI hangs off the existing `ticket()`/`state()` signals.
- One logical change per commit, conventional commit messages, on the existing
  `feat/feat-32-ticket-domain-enrichment` branch (already open from the backend work).

## File structure (whole slice)

```
frontend/projects/common/src/lib/tickets/
  ticket.api.ts                     all 4 tasks — types + TicketApi methods
  ticket.api.spec.ts                all 4 tasks — request-shape tests

frontend/projects/common/src/lib/i18n/translations.ts   all 4 tasks — new keys appended to the
                                                          existing tickets.* blocks

frontend/projects/admin-app/src/app/features/tickets/
  ticket-create.component.ts/.html/.spec.ts    Task 2 — impact/urgency replace priority
  ticket-detail.component.ts/.html/.spec.ts    Tasks 1, 2, 3, 4 — resolve form, reclassify,
                                                tag chips, links section
  ticket-queue.component.ts/.html/.spec.ts     Task 3 — tag filter + tag chips per row
```

## Tasks

Cut order matches the backend plan: **Task 4 first, then Task 3**, if time runs out.

| # | Task | Delivers | AC |
|---|---|---|---|
| 1 | Resolve inline form | resolve requires code+notes, banner shows them, reopen count visible | AC-922.7 |
| 2 | Impact/urgency create form + matrix preview | priority control replaced, live preview chip, reclassify | AC-923.7 |
| 3 | Tag chips + queue filter | add/remove chips on detail, `tag=` filter on queue | AC-924.5 |
| 4 | Links section | add/remove links on detail, directional rendering | AC-925.5 |

---

### Task 1: Resolve inline form (AC-922.7)

**Files:**
- Modify: `frontend/projects/common/src/lib/tickets/ticket.api.ts:142-168` (`TicketDetail` interface), `:286-288` (`changeStatus`)
- Modify: `frontend/projects/common/src/lib/tickets/ticket.api.spec.ts`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts:178-185` (`changeStatus`)
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html:280-303` (status action card)
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.spec.ts:171-183` (existing test breaks — fix it)
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts` (after line 790's `tickets.escalation.level3`)

**Interfaces:**
- Consumes: `ticket-detail.component.ts`'s existing `busy`, `actionError`, `run()`, `load()`,
  `toApiError()`.
- Produces: `TicketApi.changeStatus(id, status, rowVersion, resolutionCode?, resolutionNotes?)` —
  the two new params are optional and appended last, so every other call site (`Assigned`,
  `In Progress`, …) is unaffected. `TicketDetail` gains `resolutionCode: string | null`,
  `resolutionNotes: string | null`, `reopenCount: number`. `TicketDetailComponent` gains
  `readonly resolutionCodes = RESOLUTION_CODES`, `readonly showResolveForm = signal(false)`,
  `selectStatus(status: string)`, `submitResolve(code: string, notes: string)`,
  `cancelResolve()`.

- [ ] **Step 1: Write the failing API test**

Add to `frontend/projects/common/src/lib/tickets/ticket.api.spec.ts` (append near the existing
`changeStatus` test — find it with the same `describe('TicketApi'` block):

```typescript
it('changeStatus sends the resolution fields when resolving', () => {
  api.changeStatus('t-1', 'Resolved', 'AAA=', 'Fixed', 'Reset the password.').subscribe();

  const req = http.expectOne('/api/Tickets/t-1/status');
  expect(req.request.body).toEqual({
    status: 'Resolved',
    rowVersion: 'AAA=',
    resolutionCode: 'Fixed',
    resolutionNotes: 'Reset the password.',
  });
  req.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });
});

it('changeStatus omits resolution fields for a non-resolving transition', () => {
  api.changeStatus('t-1', 'Open', 'AAA=').subscribe();

  const req = http.expectOne('/api/Tickets/t-1/status');
  expect(req.request.body).toEqual({ status: 'Open', rowVersion: 'AAA=' });
  req.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });
});
```

If the spec file has no `http`/`api` fixtures at module scope, check its existing `beforeEach` and
reuse the same `TestBed.inject` pattern the other `TicketApi` tests in the file already use — do
not invent a different harness.

- [ ] **Step 2: Run to verify it fails**

```bash
cd frontend && npx ng test common --watch=false
```

Expected: FAIL — `changeStatus` still takes exactly `(id, status, rowVersion)`, so the 4th/5th
arguments are a compile error (or the request body assertion fails once compiled with `any`).

- [ ] **Step 3: Extend `TicketApi`**

In `ticket.api.ts`, extend `TicketDetail` (after `escalationAssigneeName` at line 167):

```typescript
  readonly escalationAssigneeName: string | null;
  /** US-922 / AC-922.6. Null / 0 until the ticket has been resolved / reopened. */
  readonly resolutionCode: string | null;
  readonly resolutionNotes: string | null;
  readonly reopenCount: number;
```

Add the resolution-code union near `TICKET_PRIORITIES` (after line 8):

```typescript
/** US-922. Kept in step with the backend's `TicketResolutionCode` value object. */
export const RESOLUTION_CODES = ['Fixed', 'Workaround', 'Duplicate', 'CannotReproduce', 'NoResponse'] as const;
export type ResolutionCode = (typeof RESOLUTION_CODES)[number];
```

Replace `changeStatus` (lines 286-288):

```typescript
  /**
   * Moves the ticket along its lifecycle. `rowVersion` is the value read from `get` — the server
   * compares it to detect a lost update (AC-41), so it must be echoed, not invented.
   *
   * `resolutionCode`/`resolutionNotes` are required by the server only when `status` is
   * `'Resolved'` (AC-922.1); every other transition ignores them if present, so callers simply omit
   * them (US-922).
   */
  changeStatus(
    id: string,
    status: TicketStatus,
    rowVersion: string,
    resolutionCode?: string,
    resolutionNotes?: string,
  ): Observable<unknown> {
    return this.http.post(`/api/Tickets/${id}/status`, {
      status,
      rowVersion,
      ...(resolutionCode !== undefined ? { resolutionCode, resolutionNotes } : {}),
    });
  }
```

- [ ] **Step 4: Run the API test**

```bash
cd frontend && npx ng test common --watch=false
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/projects/common/src/lib/tickets/ticket.api.ts frontend/projects/common/src/lib/tickets/ticket.api.spec.ts
git commit -m "feat(frontend): resolution fields on TicketApi.changeStatus (AC-922.7)"
```

- [ ] **Step 6: Write the failing component tests**

Replace the existing broken test in `ticket-detail.component.spec.ts` (lines 171-183,
`'AC61: a status change echoes the rowVersion it read'`) — it currently drives `changeStatus`
straight through for `'Resolved'`, which the new UI no longer does directly:

```typescript
it('AC61: a non-resolving status change echoes the rowVersion it read', async () => {
  const fixture = await render(['Agent']);

  fixture.componentInstance.selectStatus('Assigned');

  const request = http.expectOne('/api/Tickets/t-1/status');
  expect(request.request.method).toBe('POST');
  expect(request.request.body).toEqual({ status: 'Assigned', rowVersion: 'AAAAAAABAdE=' });
  request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });

  http.expectOne('/api/Tickets/t-1').flush({ success: true, code: 'CON035', message: 'OK', data: TICKET, errors: [] });
});
```

Append new tests to the same file (after the test above):

```typescript
it('AC922_7: selecting Resolved opens the inline resolve form instead of committing bare', async () => {
  const fixture = await render(['Agent']);

  fixture.componentInstance.selectStatus('Resolved');
  fixture.detectChanges();

  // No request yet — the form is showing, not submitting.
  http.expectNone('/api/Tickets/t-1/status');
  expect(fixture.componentInstance.showResolveForm()).toBe(true);
  expect(
    (fixture.nativeElement as HTMLElement).querySelector('[data-testid="resolve-form"]'),
  ).not.toBeNull();
});

it('AC922_7: submitting the resolve form sends code, notes and rowVersion', async () => {
  const fixture = await render(['Agent']);

  fixture.componentInstance.selectStatus('Resolved');
  fixture.detectChanges();
  fixture.componentInstance.submitResolve('Fixed', 'Reset the password and confirmed sign-in.');

  const request = http.expectOne('/api/Tickets/t-1/status');
  expect(request.request.body).toEqual({
    status: 'Resolved',
    rowVersion: 'AAAAAAABAdE=',
    resolutionCode: 'Fixed',
    resolutionNotes: 'Reset the password and confirmed sign-in.',
  });
  request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });

  http.expectOne('/api/Tickets/t-1').flush({
    success: true, code: 'CON035', message: 'OK',
    data: { ...TICKET, status: 'Resolved', resolutionCode: 'Fixed', resolutionNotes: 'Reset the password and confirmed sign-in.', reopenCount: 0 },
    errors: [],
  });
  fixture.detectChanges();

  expect(fixture.componentInstance.showResolveForm()).toBe(false);
  expect(
    (fixture.nativeElement as HTMLElement).querySelector('[data-testid="resolution-banner"]')?.textContent,
  ).toContain('Fixed');
});

it('AC922_7: a resolved ticket shows its resolution and reopen count', async () => {
  configure(['Agent']);
  const fixture = TestBed.createComponent(TicketDetailComponent);
  fixture.componentRef.setInput('id', 't-1');
  fixture.detectChanges();
  await Promise.resolve();

  http.expectOne('/api/Tickets/t-1').flush({
    success: true, code: 'CON035', message: 'OK',
    data: { ...TICKET, status: 'Resolved', resolutionCode: 'Workaround', resolutionNotes: 'Cleared the cache.', reopenCount: 2 },
    errors: [],
  });
  fixture.detectChanges();

  const el = fixture.nativeElement as HTMLElement;
  const banner = el.querySelector('[data-testid="resolution-banner"]');
  expect(banner?.textContent).toContain('Workaround');
  expect(banner?.textContent).toContain('Cleared the cache.');
  expect(el.querySelector('[data-testid="reopen-count"]')?.textContent).toContain('2');
});
```

- [ ] **Step 7: Run to verify failure**

```bash
cd frontend && npx ng test common --watch=false
cd frontend && npx ng test admin-app --watch=false
```

Expected: `common` passes (Step 4 already made it pass); `admin-app` FAILs — `selectStatus`,
`showResolveForm`, `submitResolve` do not exist yet, and `[data-testid="resolve-form"]` is not in
the template.

- [ ] **Step 8: Implement the component**

In `ticket-detail.component.ts`, add the import (alongside the existing `common` import block,
line 2-27):

```typescript
  RESOLUTION_CODES,
```

Add state after `readonly activeTab = signal<TicketDetailTab>('messages');` (line 73):

```typescript
  protected readonly resolutionCodes = RESOLUTION_CODES;
  readonly showResolveForm = signal(false);
```

Replace `changeStatus` (lines 178-185):

```typescript
  /**
   * AC-922.7: `Resolved` is never committed bare. Selecting it opens the inline form instead of
   * calling the API — `submitResolve` is what actually posts. Every other target still commits
   * immediately, matching the existing one-click behaviour AC-61 already established.
   */
  selectStatus(status: string): void {
    if (!status) {
      return;
    }

    if (status === 'Resolved') {
      this.showResolveForm.set(true);
      return;
    }

    this.commitStatus(status);
  }

  submitResolve(resolutionCode: string, resolutionNotes: string): void {
    const current = this.ticket();
    if (!current || this.busy() || !resolutionCode || !resolutionNotes.trim()) {
      return;
    }

    this.run(
      this.api.changeStatus(current.id, 'Resolved', current.rowVersion, resolutionCode, resolutionNotes),
    );
    this.showResolveForm.set(false);
  }

  cancelResolve(): void {
    this.showResolveForm.set(false);
  }

  private commitStatus(status: string): void {
    const current = this.ticket();
    if (!current || this.busy()) {
      return;
    }

    this.run(this.api.changeStatus(current.id, status as TicketStatus, current.rowVersion));
  }
```

In `ticket-detail.component.html`, replace the status `<select>` block (lines 282-302) — the
`(change)` handler now calls `selectStatus`, and the inline form appears beneath it:

```html
                <div class="flex flex-col gap-1.5" data-testid="status-action">
                  <label for="detail-status" class="text-label-md text-on-surface-variant">
                    {{ 'tickets.detail.moveTo' | t }}
                  </label>
                  <select
                    id="detail-status"
                    [disabled]="busy() || availableTransitions().length === 0"
                    (change)="selectStatus($any($event.target).value)"
                    class="h-10 rounded-lg border border-outline-variant bg-surface-lowest px-3 text-body-md text-on-surface transition-all focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:opacity-50"
                  >
                    <option value="">{{ 'tickets.detail.selectStatus' | t }}</option>
                    @for (option of availableTransitions(); track option) {
                      <option [value]="option">{{ option }}</option>
                    }
                  </select>
                </div>

                @if (showResolveForm()) {
                  <div
                    data-testid="resolve-form"
                    class="flex flex-col gap-3 rounded-lg border border-outline-variant bg-surface-low p-3"
                  >
                    <p class="text-label-md font-semibold text-on-surface">
                      {{ 'tickets.detail.resolve.title' | t }}
                    </p>
                    <div class="flex flex-col gap-1.5">
                      <label for="resolve-code" class="text-label-md text-on-surface-variant">
                        {{ 'tickets.detail.resolve.code' | t }}
                      </label>
                      <select
                        id="resolve-code"
                        #resolveCode
                        class="h-10 rounded-lg border border-outline-variant bg-surface-lowest px-3 text-body-md text-on-surface transition-all focus:border-primary focus:ring-2 focus:ring-primary/20"
                      >
                        <option value="">{{ 'tickets.detail.resolve.selectCode' | t }}</option>
                        @for (code of resolutionCodes; track code) {
                          <option [value]="code">{{ code }}</option>
                        }
                      </select>
                    </div>
                    <div class="flex flex-col gap-1.5">
                      <label for="resolve-notes" class="text-label-md text-on-surface-variant">
                        {{ 'tickets.detail.resolve.notes' | t }}
                      </label>
                      <textarea
                        id="resolve-notes"
                        #resolveNotes
                        rows="3"
                        class="rounded-lg border border-outline-variant bg-surface-lowest p-3 text-body-md text-on-surface transition-all focus:border-primary focus:ring-2 focus:ring-primary/20"
                      ></textarea>
                    </div>
                    <div class="flex justify-end gap-2">
                      <button
                        type="button"
                        (click)="cancelResolve()"
                        class="inline-flex h-9 items-center rounded-lg px-3 text-label-md font-semibold text-on-surface-variant transition-colors hover:bg-surface-high"
                      >
                        {{ 'action.cancel' | t }}
                      </button>
                      <button
                        type="button"
                        [disabled]="busy()"
                        (click)="submitResolve(resolveCode.value, resolveNotes.value)"
                        class="inline-flex h-9 items-center rounded-lg bg-primary px-3 text-label-md font-semibold text-on-primary transition-all hover:opacity-90 disabled:opacity-50"
                      >
                        {{ 'tickets.detail.resolve.submit' | t }}
                      </button>
                    </div>
                  </div>
                }

                @if (t.resolutionCode) {
                  <div
                    data-testid="resolution-banner"
                    class="flex flex-col gap-1 rounded-lg border border-status-resolved/20 bg-status-resolved/10 p-3"
                  >
                    <p class="text-label-md font-semibold text-status-resolved">{{ t.resolutionCode }}</p>
                    <p class="text-body-sm text-on-surface-variant">{{ t.resolutionNotes }}</p>
                    @if (t.reopenCount > 0) {
                      <p data-testid="reopen-count" class="text-body-sm text-on-surface-variant">
                        {{ 'tickets.detail.resolve.reopenCount' | t: t.reopenCount }}
                      </p>
                    }
                  </div>
                }
```

> Template-reference variables (`#resolveCode`, `#resolveNotes`) rather than a `FormGroup`: this
> block is intentionally the same lightweight pattern the assign/escalation `<select>`s already use
> (no reactive form wrapping the whole detail screen) — consistent with the file as it stands, not
> a new form architecture for one field.

Add the four i18n keys to `translations.ts`, immediately after `'tickets.escalation.level3'`
(line 790):

```typescript
  'tickets.detail.resolve.title': { en: 'Resolve this ticket', ar: 'حل هذه التذكرة' },
  'tickets.detail.resolve.code': { en: 'Resolution', ar: 'طريقة الحل' },
  'tickets.detail.resolve.selectCode': { en: 'Select a resolution', ar: 'اختر طريقة الحل' },
  'tickets.detail.resolve.notes': { en: 'Resolution notes', ar: 'ملاحظات الحل' },
  'tickets.detail.resolve.submit': { en: 'Mark resolved', ar: 'وضع علامة كمحلولة' },
  'tickets.detail.resolve.reopenCount': { en: 'Reopened {0} time(s)', ar: 'أُعيد فتحها {0} مرة' },
```

- [ ] **Step 9: Run the tests**

```bash
cd frontend && npx ng test admin-app --watch=false
```

Expected: PASS for every test in the file, including the 3 new ones and the fixed
`'AC61: a non-resolving status change...'`.

- [ ] **Step 10: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.spec.ts frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat(frontend): inline resolve form, resolution banner, reopen count (AC-922.7)"
```

---

### Task 2: Impact/urgency create form + matrix preview, reclassify (AC-923.7)

**Files:**
- Modify: `frontend/projects/common/src/lib/tickets/ticket.api.ts:6-8` (priority constants stay — still used for display), `:46-52` (`CreateTicketRequest`), add `PRIORITY_MATRIX` + `reclassify` method
- Modify: `frontend/projects/common/src/lib/tickets/ticket.api.spec.ts`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.ts` (form control, no more `priority`)
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.html:102-123` (priority select → impact/urgency selects + preview)
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.spec.ts` (`fillValid` and the field-count test)
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts` / `.html` / `.spec.ts` (reclassify control, small addition beside the resolve work)
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts`

**Interfaces:**
- Consumes: Task 1's `run()`/`busy()`/`actionError()` pattern in `ticket-detail.component.ts`.
- Produces: `TicketApi.create(request: CreateTicketRequest)` where `CreateTicketRequest` drops
  `priority` and gains `impact: TicketImpact`, `urgency: TicketUrgency`. `TICKET_IMPACTS`,
  `TICKET_URGENCIES` (`['Low','Medium','High']`), `TicketImpact`, `TicketUrgency` types.
  `derivePriority(impact, urgency): TicketPriority` — a pure client-side mirror of the matrix, for
  the preview chip only (server value is authoritative, per spec). `TicketApi.reclassify(id,
  impact, urgency, rowVersion)`. `TicketDetail` gains `impact: string | null`, `urgency: string |
  null`. `TicketDetailComponent` gains `reclassify(impact: string, urgency: string)`.

- [ ] **Step 1: Write the failing API tests**

Append to `ticket.api.spec.ts`:

```typescript
it('derivePriority matches every cell of the spec matrix', () => {
  expect(derivePriority('Low', 'Low')).toBe('Low');
  expect(derivePriority('Low', 'Medium')).toBe('Low');
  expect(derivePriority('Low', 'High')).toBe('Normal');
  expect(derivePriority('Medium', 'Low')).toBe('Low');
  expect(derivePriority('Medium', 'Medium')).toBe('Normal');
  expect(derivePriority('Medium', 'High')).toBe('High');
  expect(derivePriority('High', 'Low')).toBe('Normal');
  expect(derivePriority('High', 'Medium')).toBe('High');
  expect(derivePriority('High', 'High')).toBe('Urgent');
});

it('create sends impact and urgency, not priority', () => {
  api.create({
    subject: 'Cannot sign in',
    description: 'desc',
    customerId: 'c-1',
    categoryId: 'cat-1',
    impact: 'High',
    urgency: 'High',
  }).subscribe();

  const req = http.expectOne('/api/Tickets');
  expect(req.request.body).toEqual({
    subject: 'Cannot sign in',
    description: 'desc',
    customerId: 'c-1',
    categoryId: 'cat-1',
    impact: 'High',
    urgency: 'High',
  });
  req.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });
});

it('reclassify sends impact, urgency and rowVersion', () => {
  api.reclassify('t-1', 'High', 'High', 'AAA=').subscribe();

  const req = http.expectOne('/api/Tickets/t-1/classification');
  expect(req.request.method).toBe('POST');
  expect(req.request.body).toEqual({ impact: 'High', urgency: 'High', rowVersion: 'AAA=' });
  req.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });
});
```

Add the import at the top of the spec file: `import { TicketApi, derivePriority } from 'common';`
(adjust to match however the file currently imports `TicketApi` — keep one import line, don't
duplicate).

- [ ] **Step 2: Run to verify failure**

```bash
cd frontend && npx ng test common --watch=false
```

Expected: FAIL — `derivePriority`, `reclassify` do not exist; `CreateTicketRequest` still requires
`priority` and rejects `impact`/`urgency` under the TypeScript compiler (test file fails to build).

- [ ] **Step 3: Extend `TicketApi`**

Replace the priority-constants block (lines 6-8) — keep `TICKET_PRIORITIES`/`TicketPriority` (still
returned by the server and displayed via `cs-badge`), add the matrix inputs and the pure
derivation:

```typescript
/** The four values `TicketPriority` allows. Kept in step with the backend value object. */
export const TICKET_PRIORITIES = ['Low', 'Normal', 'High', 'Urgent'] as const;
export type TicketPriority = (typeof TICKET_PRIORITIES)[number];

/** US-923. The matrix inputs — priority is derived from these, never set directly. */
export const TICKET_IMPACTS = ['Low', 'Medium', 'High'] as const;
export type TicketImpact = (typeof TICKET_IMPACTS)[number];
export const TICKET_URGENCIES = ['Low', 'Medium', 'High'] as const;
export type TicketUrgency = (typeof TICKET_URGENCIES)[number];

/**
 * A client-side **preview** of the server's matrix (spec decision 2026-08-31: matrix-only
 * priority). Display only — the create/reclassify response carries the authoritative value, and
 * this must never be sent back to the server as if it were an input.
 */
const PRIORITY_MATRIX: Readonly<Record<TicketImpact, Readonly<Record<TicketUrgency, TicketPriority>>>> = {
  Low: { Low: 'Low', Medium: 'Low', High: 'Normal' },
  Medium: { Low: 'Low', Medium: 'Normal', High: 'High' },
  High: { Low: 'Normal', Medium: 'High', High: 'Urgent' },
};

export function derivePriority(impact: TicketImpact, urgency: TicketUrgency): TicketPriority {
  return PRIORITY_MATRIX[impact][urgency];
}
```

Replace `CreateTicketRequest` (lines 46-52):

```typescript
export interface CreateTicketRequest {
  readonly subject: string;
  readonly description: string;
  readonly customerId: string;
  readonly categoryId: string;
  readonly impact: TicketImpact;
  readonly urgency: TicketUrgency;
}
```

Extend `TicketDetail` (after Task 1's `reopenCount`):

```typescript
  /** US-923 / AC-923.6. Null on tickets created before FEAT-32 (spec A1). */
  readonly impact: string | null;
  readonly urgency: string | null;
```

Add `reclassify`, next to `changeStatus`:

```typescript
  /**
   * US-923 / AC-923.2. Sets the matrix inputs; the server re-derives priority. There is no
   * endpoint that sets priority directly — this is the only mutation path it has post-creation.
   */
  reclassify(id: string, impact: TicketImpact, urgency: TicketUrgency, rowVersion: string): Observable<unknown> {
    return this.http.post(`/api/Tickets/${id}/classification`, { impact, urgency, rowVersion });
  }
```

- [ ] **Step 4: Run the API tests**

```bash
cd frontend && npx ng test common --watch=false
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/projects/common/src/lib/tickets/ticket.api.ts frontend/projects/common/src/lib/tickets/ticket.api.spec.ts
git commit -m "feat(frontend): impact/urgency matrix on TicketApi (AC-923.7)"
```

- [ ] **Step 6: Write the failing create-form tests**

In `ticket-create.component.spec.ts`, replace `fillValid` (lines 74-82):

```typescript
  function fillValid(fixture: ComponentFixture<TicketCreateComponent>) {
    fixture.componentInstance.form.setValue({
      subject: 'Cannot sign in',
      description: 'The portal rejects my password.',
      customerId: 'c-1',
      categoryId: 'cat-1',
      impact: 'Medium',
      urgency: 'Medium',
    });
  }
```

Replace the field-count assertion in `'AC418_TicketFormsAndActionsAreKeyboardAccessible'` (line
219, currently `expect(el.querySelectorAll('select').length).toBe(3);`) with `toBe(4)` — customer,
category, impact, urgency.

Append a new test:

```typescript
it('AC923_7: create sends impact and urgency, and shows the derived priority preview', () => {
  const fixture = render();
  fillValid(fixture);
  fixture.componentInstance.form.controls.impact.setValue('High');
  fixture.componentInstance.form.controls.urgency.setValue('High');
  fixture.detectChanges();

  expect(
    (fixture.nativeElement as HTMLElement).querySelector('[data-testid="priority-preview"]')?.textContent,
  ).toContain('Urgent');

  fixture.componentInstance.submit();

  const req = http.expectOne('/api/Tickets');
  expect(req.request.body).toEqual({
    subject: 'Cannot sign in',
    description: 'The portal rejects my password.',
    customerId: 'c-1',
    categoryId: 'cat-1',
    impact: 'High',
    urgency: 'High',
  });
  req.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });
});
```

- [ ] **Step 7: Run to verify failure**

```bash
cd frontend && npx ng test admin-app --watch=false
```

Expected: FAIL — `form.controls.impact` does not exist; the `priority` control still does; the
select count is 3, not 4.

- [ ] **Step 8: Implement the create form**

In `ticket-create.component.ts`, replace the `common` import list's priority symbols and the form:

```typescript
import {
  ApiError,
  CategoryOption,
  CsActionBar,
  CsAttachmentPicker,
  CsButton,
  CsCard,
  CsIcon,
  CsInputField,
  CustomerOption,
  derivePriority,
  LocaleStore,
  TICKET_IMPACTS,
  TICKET_URGENCIES,
  TicketApi,
  TicketImpact,
  TicketUrgency,
  TranslatePipe,
} from 'common';
```

```typescript
  protected readonly impacts = TICKET_IMPACTS;
  protected readonly urgencies = TICKET_URGENCIES;
```

Replace the `priority` control in `form` with two controls:

```typescript
  readonly form = new FormGroup({
    subject: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    customerId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    categoryId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    impact: new FormControl<TicketImpact>('Medium', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    urgency: new FormControl<TicketUrgency>('Medium', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  /**
   * AC-923.7 — a client-side mirror of the matrix, display only; the server derives the real
   * value. A plain writable signal, not `computed`: Reactive Forms values are not observed
   * reactively without an explicit subscription, so the constructor below re-derives it on every
   * `impact`/`urgency` change.
   */
  readonly derivedPriority = signal<ReturnType<typeof derivePriority>>(derivePriority('Medium', 'Medium'));
```

Add to the constructor, before the existing `this.api.listCategories()` call:

```typescript
  constructor() {
    const recompute = () =>
      this.derivedPriority.set(
        derivePriority(this.form.controls.impact.value, this.form.controls.urgency.value),
      );
    this.form.controls.impact.valueChanges.subscribe(recompute);
    this.form.controls.urgency.valueChanges.subscribe(recompute);

    this.api.listCategories().subscribe({
      // ... unchanged ...
```

In `ticket-create.component.html`, replace the priority `<select>` block (lines 102-123) with two
selects plus the preview chip:

```html
        <div class="flex flex-col gap-1.5">
          <label for="ticket-impact" class="text-label-md text-on-surface-variant">
            {{ 'field.impact' | t }}
          </label>
          <select
            id="ticket-impact"
            formControlName="impact"
            [attr.aria-invalid]="fieldError('impact') ? 'true' : null"
            [attr.aria-describedby]="fieldError('impact') ? 'ticket-impact-error' : null"
            (change)="clearServerError('impact')"
            class="h-10 rounded-lg border border-outline-variant bg-surface-lowest px-3 text-body-md text-on-surface transition-all focus:border-primary focus:ring-2 focus:ring-primary/20"
          >
            @for (impact of impacts; track impact) {
              <option [value]="impact">{{ impact }}</option>
            }
          </select>
          @if (fieldError('impact'); as failure) {
            <p id="ticket-impact-error" role="alert" class="text-body-sm text-error">
              {{ failure.message }}
            </p>
          }
        </div>

        <div class="flex flex-col gap-1.5">
          <label for="ticket-urgency" class="text-label-md text-on-surface-variant">
            {{ 'field.urgency' | t }}
          </label>
          <select
            id="ticket-urgency"
            formControlName="urgency"
            [attr.aria-invalid]="fieldError('urgency') ? 'true' : null"
            [attr.aria-describedby]="fieldError('urgency') ? 'ticket-urgency-error' : null"
            (change)="clearServerError('urgency')"
            class="h-10 rounded-lg border border-outline-variant bg-surface-lowest px-3 text-body-md text-on-surface transition-all focus:border-primary focus:ring-2 focus:ring-primary/20"
          >
            @for (urgency of urgencies; track urgency) {
              <option [value]="urgency">{{ urgency }}</option>
            }
          </select>
          @if (fieldError('urgency'); as failure) {
            <p id="ticket-urgency-error" role="alert" class="text-body-sm text-error">
              {{ failure.message }}
            </p>
          }
        </div>

        <div class="flex flex-col gap-1.5">
          <span class="text-label-md text-on-surface-variant">{{ 'field.priority' | t }}</span>
          <div
            data-testid="priority-preview"
            class="flex h-10 items-center rounded-lg border border-dashed border-outline-variant bg-surface-low px-3"
          >
            <cs-badge kind="priority" [value]="derivedPriority()" />
          </div>
        </div>
```

Add the i18n keys (`field.impact`, `field.urgency`) near the existing `'field.priority'` entry
(line 583 of `translations.ts`):

```typescript
  'field.impact': { en: 'Impact', ar: 'التأثير' },
  'field.urgency': { en: 'Urgency', ar: 'الإلحاح' },
```

- [ ] **Step 9: Run the create-form tests**

```bash
cd frontend && npx ng test admin-app --watch=false
```

Expected: PASS.

- [ ] **Step 10: Add reclassify to the detail screen**

In `ticket-detail.component.ts`, add after `submitResolve`/`cancelResolve`:

```typescript
  reclassify(impact: string, urgency: string): void {
    const current = this.ticket();
    if (!current || this.busy() || !impact || !urgency) {
      return;
    }

    this.run(this.api.reclassify(current.id, impact as TicketImpact, urgency as TicketUrgency, current.rowVersion));
  }
```

Add `TicketImpact, TicketUrgency, TICKET_IMPACTS, TICKET_URGENCIES` to the `common` import list and
`protected readonly impacts = TICKET_IMPACTS;` / `protected readonly urgencies = TICKET_URGENCIES;`
alongside `resolutionCodes`.

In `ticket-detail.component.html`, add a small reclassify control inside the same status `cs-card`
as the assign/escalation selects (after the escalation-owner block, before `@if (actionError())`):

```html
                <div class="flex flex-col gap-1.5" data-testid="reclassify-action">
                  <label for="detail-impact" class="text-label-md text-on-surface-variant">
                    {{ 'tickets.detail.reclassify' | t }}
                  </label>
                  <div class="flex gap-2">
                    <select
                      id="detail-impact"
                      #impactSelect
                      [disabled]="busy()"
                      class="h-10 flex-1 rounded-lg border border-outline-variant bg-surface-lowest px-3 text-body-md text-on-surface transition-all focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:opacity-50"
                    >
                      @for (impact of impacts; track impact) {
                        <option [value]="impact" [selected]="impact === t.impact">{{ impact }}</option>
                      }
                    </select>
                    <select
                      id="detail-urgency"
                      #urgencySelect
                      [disabled]="busy()"
                      class="h-10 flex-1 rounded-lg border border-outline-variant bg-surface-lowest px-3 text-body-md text-on-surface transition-all focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:opacity-50"
                    >
                      @for (urgency of urgencies; track urgency) {
                        <option [value]="urgency" [selected]="urgency === t.urgency">{{ urgency }}</option>
                      }
                    </select>
                    <button
                      type="button"
                      [disabled]="busy()"
                      (click)="reclassify(impactSelect.value, urgencySelect.value)"
                      class="inline-flex h-10 items-center rounded-lg border border-outline-variant px-3 text-label-md font-semibold text-on-surface transition-colors hover:bg-surface-high disabled:opacity-50"
                    >
                      {{ 'action.apply' | t }}
                    </button>
                  </div>
                </div>
```

Add the i18n key: `'tickets.detail.reclassify': { en: 'Impact / urgency', ar: 'التأثير / الإلحاح' },`
after `tickets.detail.resolve.reopenCount`. Check `action.apply` doesn't already exist under a
different key before adding it (`grep "'action.apply'" translations.ts`); if it's missing, add
`'action.apply': { en: 'Apply', ar: 'تطبيق' },` next to the existing `'action.cancel'` entry.

- [ ] **Step 11: Write and run a reclassify test**

Append to `ticket-detail.component.spec.ts`:

```typescript
it('AC923_7: reclassify posts impact, urgency and rowVersion', async () => {
  const fixture = await render(['Agent']);

  fixture.componentInstance.reclassify('High', 'High');

  const request = http.expectOne('/api/Tickets/t-1/classification');
  expect(request.request.body).toEqual({ impact: 'High', urgency: 'High', rowVersion: 'AAAAAAABAdE=' });
  request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });

  http.expectOne('/api/Tickets/t-1').flush({ success: true, code: 'CON035', message: 'OK', data: { ...TICKET, impact: 'High', urgency: 'High' }, errors: [] });
});
```

```bash
cd frontend && npx ng test admin-app --watch=false
```

Expected: PASS.

- [ ] **Step 12: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.ts frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.html frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.spec.ts frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.spec.ts frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat(frontend): impact/urgency create form, derived-priority preview, reclassify (AC-923.7)"
```

---

### Task 3: Tag chips + queue filter (AC-924.5)

**Files:**
- Modify: `frontend/projects/common/src/lib/tickets/ticket.api.ts` (`TicketListItem`, `TicketDetail`, `TicketFilters`, two new methods)
- Modify: `frontend/projects/common/src/lib/tickets/ticket.api.spec.ts`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts` / `.html` / `.spec.ts`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.ts` / `.html` / `.spec.ts`
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts`

**Interfaces:**
- Consumes: Task 1/2's `run()` pattern; `ticket-queue.component.ts`'s existing `load()`,
  `status`/`mine`/`page` signal pattern.
- Produces: `TicketApi.addTag(id, value)`, `TicketApi.removeTag(id, value)`. `TicketListItem` and
  `TicketDetail` gain `tags: readonly string[]`. `TicketFilters` gains `tag?: string | null`.
  `TicketDetailComponent` gains `addTag(value: string)`, `removeTag(value: string)`,
  `newTagValue = signal('')`. `TicketQueueComponent` gains `tagFilter = signal('')`,
  `setTagFilter(value: string)`.

- [ ] **Step 1: Write the failing API tests**

Append to `ticket.api.spec.ts`:

```typescript
it('addTag posts the raw value', () => {
  api.addTag('t-1', 'Billing Issue').subscribe();

  const req = http.expectOne('/api/Tickets/t-1/tags');
  expect(req.request.method).toBe('POST');
  expect(req.request.body).toEqual({ value: 'Billing Issue' });
  req.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });
});

it('removeTag deletes by the normalized value in the route', () => {
  api.removeTag('t-1', 'billing').subscribe();

  const req = http.expectOne('/api/Tickets/t-1/tags/billing');
  expect(req.request.method).toBe('DELETE');
  req.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });
});

it('list sends the tag filter only when set', () => {
  api.list({ tag: 'billing' }).subscribe();
  http.expectOne((r) => r.url === '/api/Tickets' && r.params.get('tag') === 'billing')
    .flush({ items: [], totalCount: 0 });
});
```

- [ ] **Step 2: Run to verify failure**

```bash
cd frontend && npx ng test common --watch=false
```

Expected: FAIL — `addTag`/`removeTag` do not exist; `tag` is not a valid `TicketFilters` key.

- [ ] **Step 3: Extend `TicketApi`**

Add `tags` to `TicketListItem` (after `escalationState`, line 34) and `TicketDetail` (after Task
2's `urgency`):

```typescript
  /** US-924 / AC-924.4. Normalized values, alphabetical; empty when untagged. */
  readonly tags: readonly string[];
```

Add `tag` to `TicketFilters` (after `unassigned`, line 43):

```typescript
  /** US-924 / AC-924.4. Only tickets carrying this tag. */
  readonly tag?: string | null;
```

In `list()`, add after the `unassigned` block (line 218):

```typescript
    if (filters.tag) {
      params = params.set('tag', filters.tag);
    }
```

Add the two new methods, next to `recordMessage` (end of class, before the closing brace at line
311):

```typescript
  /** US-924 / AC-924.1. The server normalizes and validates (charset, length, duplicate, limit). */
  addTag(id: string, value: string): Observable<unknown> {
    return this.http.post(`/api/Tickets/${id}/tags`, { value });
  }

  /** US-924. `value` should be the already-normalized tag as rendered — the route carries it raw. */
  removeTag(id: string, value: string): Observable<unknown> {
    return this.http.delete(`/api/Tickets/${id}/tags/${encodeURIComponent(value)}`);
  }
```

- [ ] **Step 4: Run the API tests, commit**

```bash
cd frontend && npx ng test common --watch=false
git add frontend/projects/common/src/lib/tickets/ticket.api.ts frontend/projects/common/src/lib/tickets/ticket.api.spec.ts
git commit -m "feat(frontend): tag endpoints and tag filter on TicketApi (AC-924.5)"
```

- [ ] **Step 5: Write the failing detail-screen tag tests**

Append to `ticket-detail.component.spec.ts`:

```typescript
it('AC924_5: adding a tag posts the value and re-reads the ticket', async () => {
  const fixture = await render(['Agent']);

  fixture.componentInstance.newTagValue.set('billing');
  fixture.componentInstance.addTag('billing');

  const request = http.expectOne('/api/Tickets/t-1/tags');
  expect(request.request.body).toEqual({ value: 'billing' });
  request.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });

  http.expectOne('/api/Tickets/t-1').flush({
    success: true, code: 'CON035', message: 'OK', data: { ...TICKET, tags: ['billing'] }, errors: [],
  });
  fixture.detectChanges();

  expect(fixture.componentInstance.newTagValue()).toBe('');
  const chips = (fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="tag-chip"]');
  expect(chips.length).toBe(1);
  expect(chips[0].textContent).toContain('billing');
});

it('AC924_5: removing a tag deletes it', async () => {
  configure(['Agent']);
  const fixture = TestBed.createComponent(TicketDetailComponent);
  fixture.componentRef.setInput('id', 't-1');
  fixture.detectChanges();
  await Promise.resolve();
  http.expectOne('/api/Tickets/t-1').flush({
    success: true, code: 'CON035', message: 'OK', data: { ...TICKET, tags: ['billing'] }, errors: [],
  });
  fixture.detectChanges();

  fixture.componentInstance.removeTag('billing');

  const request = http.expectOne('/api/Tickets/t-1/tags/billing');
  expect(request.request.method).toBe('DELETE');
  request.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });

  http.expectOne('/api/Tickets/t-1').flush({
    success: true, code: 'CON035', message: 'OK', data: { ...TICKET, tags: [] }, errors: [],
  });
});
```

- [ ] **Step 6: Run to verify failure**

```bash
cd frontend && npx ng test admin-app --watch=false
```

Expected: FAIL — `newTagValue`, `addTag`, `removeTag` do not exist on the component.

- [ ] **Step 7: Implement the detail-screen tag chips**

In `ticket-detail.component.ts`, add state after `readonly showResolveForm = signal(false);`:

```typescript
  readonly newTagValue = signal('');
```

Add methods after `reclassify`:

```typescript
  addTag(value: string): void {
    const current = this.ticket();
    const trimmed = value.trim();
    if (!current || this.busy() || !trimmed) {
      return;
    }

    this.run(this.api.addTag(current.id, trimmed));
    this.newTagValue.set('');
  }

  removeTag(value: string): void {
    const current = this.ticket();
    if (!current || this.busy()) {
      return;
    }

    this.run(this.api.removeTag(current.id, value));
  }
```

In `ticket-detail.component.html`, add a tags card in the right column, after the status `cs-card`
closes (after the `</cs-card>` that ends the status block, before the AI panel comment):

```html
            <cs-card [heading]="'tickets.detail.tags' | t">
              <div class="flex flex-col gap-3 p-4">
                <div class="flex flex-wrap gap-2" data-testid="tag-list">
                  @for (tag of t.tags; track tag) {
                    <span
                      data-testid="tag-chip"
                      class="inline-flex items-center gap-1 rounded-full border border-outline-variant bg-surface-low px-2.5 py-1 text-label-md text-on-surface"
                    >
                      {{ tag }}
                      <button
                        type="button"
                        [attr.aria-label]="'tickets.detail.removeTag' | t: tag"
                        [disabled]="busy()"
                        (click)="removeTag(tag)"
                        class="grid size-4 place-items-center rounded-full text-on-surface-variant hover:bg-surface-highest hover:text-on-surface disabled:opacity-50"
                      >
                        <cs-icon name="close" [size]="12" />
                      </button>
                    </span>
                  } @empty {
                    <p class="text-body-sm text-on-surface-variant">{{ 'tickets.detail.noTags' | t }}</p>
                  }
                </div>
                <div class="flex gap-2">
                  <input
                    type="text"
                    [value]="newTagValue()"
                    (input)="newTagValue.set($any($event.target).value)"
                    (keydown.enter)="$event.preventDefault(); addTag(newTagValue())"
                    [attr.aria-label]="'tickets.detail.addTag' | t"
                    [placeholder]="'tickets.detail.addTag' | t"
                    [disabled]="busy() || t.tags.length >= 10"
                    class="h-9 flex-1 rounded-lg border border-outline-variant bg-surface-lowest px-3 text-body-sm text-on-surface transition-all focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:opacity-50"
                  />
                  <button
                    type="button"
                    [disabled]="busy() || !newTagValue().trim() || t.tags.length >= 10"
                    (click)="addTag(newTagValue())"
                    class="inline-flex h-9 items-center rounded-lg border border-outline-variant px-3 text-label-md font-semibold text-on-surface transition-colors hover:bg-surface-high disabled:opacity-50"
                  >
                    {{ 'action.add' | t }}
                  </button>
                </div>
                @if (t.tags.length >= 10) {
                  <p class="text-body-sm text-on-surface-variant">{{ 'tickets.detail.tagLimit' | t }}</p>
                }
              </div>
            </cs-card>
```

Add i18n keys after `tickets.detail.reclassify`:

```typescript
  'tickets.detail.tags': { en: 'Tags', ar: 'الوسوم' },
  'tickets.detail.noTags': { en: 'No tags yet.', ar: 'لا توجد وسوم بعد.' },
  'tickets.detail.addTag': { en: 'Add a tag', ar: 'أضف وسمًا' },
  'tickets.detail.removeTag': { en: 'Remove tag {0}', ar: 'إزالة الوسم {0}' },
  'tickets.detail.tagLimit': { en: 'A ticket cannot carry more than 10 tags.', ar: 'لا يمكن أن تحمل التذكرة أكثر من 10 وسوم.' },
```

Check `'action.add'` doesn't already exist elsewhere with different copy before adding it next to
`'action.cancel'`.

- [ ] **Step 8: Run the detail-screen tag tests**

```bash
cd frontend && npx ng test admin-app --watch=false
```

Expected: PASS.

- [ ] **Step 9: Write and run the queue filter test**

Read `ticket-queue.component.spec.ts` first to match its existing `render()`/fixture setup before
appending (do not assume the exact helper name — mirror the file's own pattern for driving
`load()` and flushing `/api/Tickets`). Add:

```typescript
it('AC924_5: setting a tag filter re-queries with the tag parameter', () => {
  const fixture = render(); // however the file's own setup function is named
  http.expectOne((r) => r.url === '/api/Tickets').flush({ items: [], totalCount: 0 });
  fixture.detectChanges();

  fixture.componentInstance.setTagFilter('billing');

  http.expectOne((r) => r.url === '/api/Tickets' && r.params.get('tag') === 'billing')
    .flush({ items: [], totalCount: 0 });
});
```

In `ticket-queue.component.ts`, add:

```typescript
  readonly tagFilter = signal('');
```

In `load()`, extend the `.list(...)` call:

```typescript
    this.api
      .list({ page: this.page(), pageSize: 10, status: this.status(), mine: this.mine(), tag: this.tagFilter() || null })
      .subscribe({
```

Add:

```typescript
  setTagFilter(value: string): void {
    this.tagFilter.set(value);
    this.page.set(1);
    this.load();
  }
```

In `ticket-queue.component.html`, add a tag filter input in the checkbox row (after the
`sortByEscalation` label, still inside the same `flex flex-wrap items-center gap-4` div):

```html
      <div class="flex items-center gap-2">
        <label for="queue-tag-filter" class="text-label-md text-on-surface-variant">
          {{ 'tickets.queue.tagFilter' | t }}
        </label>
        <input
          id="queue-tag-filter"
          type="text"
          [value]="tagFilter()"
          (change)="setTagFilter($any($event.target).value)"
          [placeholder]="'tickets.queue.tagFilterPlaceholder' | t"
          class="h-8 w-40 rounded-lg border border-outline-variant bg-surface-lowest px-2 text-body-sm text-on-surface transition-all focus:border-primary focus:ring-2 focus:ring-primary/20"
        />
      </div>
```

Add a tag chip strip under the assignee cell in the table row (inside the existing assignee `<td>`,
after the closing `</span>` of the assignee block, still inside the same `<td>`):

```html
                      @if (ticket.tags.length > 0) {
                        <span class="ms-2 flex flex-wrap gap-1">
                          @for (tag of ticket.tags; track tag) {
                            <span class="rounded-full bg-surface-highest px-1.5 py-0.5 text-label-md text-on-surface-variant">
                              {{ tag }}
                            </span>
                          }
                        </span>
                      }
```

Add i18n keys after `'tickets.queue.sortByEscalation'`:

```typescript
  'tickets.queue.tagFilter': { en: 'Tag', ar: 'الوسم' },
  'tickets.queue.tagFilterPlaceholder': { en: 'Filter by tag', ar: 'تصفية حسب الوسم' },
```

```bash
cd frontend && npx ng test admin-app --watch=false
```

Expected: PASS.

- [ ] **Step 10: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.spec.ts frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.ts frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.html frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.spec.ts frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat(frontend): tag chip editor and queue tag filter (AC-924.5)"
```

---

### Task 4: Links section (AC-925.5)

**Files:**
- Modify: `frontend/projects/common/src/lib/tickets/ticket.api.ts` (`TicketLink` type, `TicketDetail`, two new methods)
- Modify: `frontend/projects/common/src/lib/tickets/ticket.api.spec.ts`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts` / `.html` / `.spec.ts`
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts`

**Interfaces:**
- Consumes: Task 1/2/3's `run()` pattern.
- Produces: `TicketApi.addLink(id, linkType, targetReference)`,
  `TicketApi.removeLink(id, linkId)`. `TicketLink` interface. `TicketDetail` gains `links: readonly
  TicketLink[]`. `TicketDetailComponent` gains `addLink(linkType: string, targetReference: string)`,
  `removeLink(linkId: string)`.

- [ ] **Step 1: Write the failing API tests**

Append to `ticket.api.spec.ts`:

```typescript
it('addLink posts linkType and targetReference', () => {
  api.addLink('t-1', 'RelatedTo', 'TKT-002000').subscribe();

  const req = http.expectOne('/api/Tickets/t-1/links');
  expect(req.request.method).toBe('POST');
  expect(req.request.body).toEqual({ linkType: 'RelatedTo', targetReference: 'TKT-002000' });
  req.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });
});

it('removeLink deletes by link id', () => {
  api.removeLink('t-1', 'link-1').subscribe();

  const req = http.expectOne('/api/Tickets/t-1/links/link-1');
  expect(req.request.method).toBe('DELETE');
  req.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });
});
```

- [ ] **Step 2: Run to verify failure**

```bash
cd frontend && npx ng test common --watch=false
```

Expected: FAIL — `addLink`/`removeLink` do not exist.

- [ ] **Step 3: Extend `TicketApi`**

Add the type near `TicketHistoryEntry` (before `TicketDetail`, around line 141):

```typescript
/**
 * One edge of the link graph as seen from this ticket (US-925, AC-925.5). `direction` is
 * `'Outbound'` when this ticket is the source ("duplicate of ..."), `'Inbound'` when it is the
 * target ("duplicated by ...").
 */
export interface TicketLink {
  readonly id: string;
  readonly linkType: 'RelatedTo' | 'DuplicateOf';
  readonly direction: 'Outbound' | 'Inbound';
  readonly otherTicketId: string;
  readonly otherReference: string;
  readonly otherSubject: string;
}
```

Add `links` to `TicketDetail` (after Task 3's `tags`):

```typescript
  /** US-925. */
  readonly links: readonly TicketLink[];
```

Add the two methods, after Task 3's `removeTag`:

```typescript
  /** US-925 / AC-925.1. `targetReference` is the other ticket's TKT-nnnnnn reference. */
  addLink(id: string, linkType: 'RelatedTo' | 'DuplicateOf', targetReference: string): Observable<unknown> {
    return this.http.post(`/api/Tickets/${id}/links`, { linkType, targetReference });
  }

  /** US-925 / AC-925.4. */
  removeLink(id: string, linkId: string): Observable<unknown> {
    return this.http.delete(`/api/Tickets/${id}/links/${linkId}`);
  }
```

- [ ] **Step 4: Run the API tests, commit**

```bash
cd frontend && npx ng test common --watch=false
git add frontend/projects/common/src/lib/tickets/ticket.api.ts frontend/projects/common/src/lib/tickets/ticket.api.spec.ts
git commit -m "feat(frontend): link endpoints on TicketApi (AC-925.5)"
```

- [ ] **Step 5: Write the failing detail-screen link tests**

Append to `ticket-detail.component.spec.ts`:

```typescript
it('AC925_5: adding a link posts type and target reference', async () => {
  const fixture = await render(['Agent']);

  fixture.componentInstance.addLink('RelatedTo', 'TKT-002000');

  const request = http.expectOne('/api/Tickets/t-1/links');
  expect(request.request.body).toEqual({ linkType: 'RelatedTo', targetReference: 'TKT-002000' });
  request.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });

  http.expectOne('/api/Tickets/t-1').flush({
    success: true, code: 'CON035', message: 'OK',
    data: { ...TICKET, links: [{ id: 'l-1', linkType: 'RelatedTo', direction: 'Outbound', otherTicketId: 't-2', otherReference: 'TKT-002000', otherSubject: 'Billing question' }] },
    errors: [],
  });
  fixture.detectChanges();

  const el = fixture.nativeElement as HTMLElement;
  expect(el.querySelector('[data-testid="link-row"]')?.textContent).toContain('TKT-002000');
});

it('AC925_5: a DuplicateOf link renders directionally', async () => {
  configure(['Agent']);
  const fixture = TestBed.createComponent(TicketDetailComponent);
  fixture.componentRef.setInput('id', 't-1');
  fixture.detectChanges();
  await Promise.resolve();
  http.expectOne('/api/Tickets/t-1').flush({
    success: true, code: 'CON035', message: 'OK',
    data: { ...TICKET, links: [{ id: 'l-1', linkType: 'DuplicateOf', direction: 'Inbound', otherTicketId: 't-2', otherReference: 'TKT-002000', otherSubject: 'Same issue' }] },
    errors: [],
  });
  fixture.detectChanges();

  const row = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="link-row"]');
  expect(row?.textContent).toContain('TKT-002000');
  // Inbound DuplicateOf reads "duplicated by", not "duplicate of".
  expect(row?.textContent?.toLowerCase()).toContain('duplicated by');
});

it('AC925_5: removing a link deletes by id', async () => {
  configure(['Agent']);
  const fixture = TestBed.createComponent(TicketDetailComponent);
  fixture.componentRef.setInput('id', 't-1');
  fixture.detectChanges();
  await Promise.resolve();
  http.expectOne('/api/Tickets/t-1').flush({
    success: true, code: 'CON035', message: 'OK',
    data: { ...TICKET, links: [{ id: 'l-1', linkType: 'RelatedTo', direction: 'Outbound', otherTicketId: 't-2', otherReference: 'TKT-002000', otherSubject: 'Billing question' }] },
    errors: [],
  });
  fixture.detectChanges();

  fixture.componentInstance.removeLink('l-1');

  const request = http.expectOne('/api/Tickets/t-1/links/l-1');
  expect(request.request.method).toBe('DELETE');
  request.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });

  http.expectOne('/api/Tickets/t-1').flush({
    success: true, code: 'CON035', message: 'OK', data: { ...TICKET, links: [] }, errors: [],
  });
});
```

Every existing fixture object literal for the ticket in this spec file (`TICKET`, and the ad-hoc
`{ ...TICKET, ... }` overrides used above) needs `tags: []` and `links: []` added to `TICKET`
itself (Task 3 already required `tags`; this task adds `links`) so every test that does not
override them still satisfies the `TicketDetail` type.

- [ ] **Step 6: Run to verify failure**

```bash
cd frontend && npx ng test admin-app --watch=false
```

Expected: FAIL — `addLink`/`removeLink` do not exist on the component; no `[data-testid="link-row"]`
in the template.

- [ ] **Step 7: Implement the links section**

In `ticket-detail.component.ts`, add state near `newTagValue`:

```typescript
  readonly newLinkType = signal<'RelatedTo' | 'DuplicateOf'>('RelatedTo');
  readonly newLinkReference = signal('');
```

Add methods after `removeTag`:

```typescript
  addLink(linkType: string, targetReference: string): void {
    const current = this.ticket();
    const reference = targetReference.trim();
    if (!current || this.busy() || !reference) {
      return;
    }

    this.run(this.api.addLink(current.id, linkType as 'RelatedTo' | 'DuplicateOf', reference));
    this.newLinkReference.set('');
  }

  removeLink(linkId: string): void {
    const current = this.ticket();
    if (!current || this.busy()) {
      return;
    }

    this.run(this.api.removeLink(current.id, linkId));
  }

  /**
   * AC-925.5 — the directional reading. `RelatedTo` shows the same way from both sides; `DuplicateOf`
   * does not: the source reads "duplicate of", the target it points at reads "duplicated by".
   */
  linkLabel(link: TicketLink): string {
    if (link.linkType === 'RelatedTo') {
      return this.locale.t('tickets.detail.links.related');
    }

    return link.direction === 'Outbound'
      ? this.locale.t('tickets.detail.links.duplicateOf')
      : this.locale.t('tickets.detail.links.duplicatedBy');
  }
```

Add `TicketLink` to the `common` import list.

In `ticket-detail.component.html`, add a links card after the tags `cs-card` (before the AI panel):

```html
            <cs-card [heading]="'tickets.detail.links' | t">
              <div class="flex flex-col gap-3 p-4">
                <ul class="flex flex-col gap-2" data-testid="link-list">
                  @for (link of t.links; track link.id) {
                    <li
                      data-testid="link-row"
                      class="flex items-center justify-between gap-2 rounded-lg border border-outline-variant bg-surface-low px-3 py-2"
                    >
                      <span class="min-w-0 text-body-sm text-on-surface">
                        <span class="text-on-surface-variant">{{ linkLabel(link) }}</span>
                        <span class="font-mono text-data-mono">{{ link.otherReference }}</span>
                        — <span class="truncate">{{ link.otherSubject }}</span>
                      </span>
                      <button
                        type="button"
                        [attr.aria-label]="'tickets.detail.links.remove' | t: link.otherReference"
                        [disabled]="busy()"
                        (click)="removeLink(link.id)"
                        class="grid size-6 shrink-0 place-items-center rounded-full text-on-surface-variant hover:bg-surface-highest hover:text-on-surface disabled:opacity-50"
                      >
                        <cs-icon name="close" [size]="14" />
                      </button>
                    </li>
                  } @empty {
                    <p class="text-body-sm text-on-surface-variant">{{ 'tickets.detail.links.none' | t }}</p>
                  }
                </ul>
                <div class="flex gap-2">
                  <select
                    #linkTypeSelect
                    [attr.aria-label]="'tickets.detail.links.type' | t"
                    class="h-9 rounded-lg border border-outline-variant bg-surface-lowest px-2 text-body-sm text-on-surface transition-all focus:border-primary focus:ring-2 focus:ring-primary/20"
                  >
                    <option value="RelatedTo">{{ 'tickets.detail.links.related' | t }}</option>
                    <option value="DuplicateOf">{{ 'tickets.detail.links.duplicateOf' | t }}</option>
                  </select>
                  <input
                    type="text"
                    #linkRefInput
                    [attr.aria-label]="'tickets.detail.links.targetPlaceholder' | t"
                    [placeholder]="'tickets.detail.links.targetPlaceholder' | t"
                    [disabled]="busy()"
                    class="h-9 flex-1 rounded-lg border border-outline-variant bg-surface-lowest px-3 text-body-sm text-on-surface transition-all focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:opacity-50"
                  />
                  <button
                    type="button"
                    [disabled]="busy()"
                    (click)="addLink(linkTypeSelect.value, linkRefInput.value); linkRefInput.value = ''"
                    class="inline-flex h-9 shrink-0 items-center rounded-lg border border-outline-variant px-3 text-label-md font-semibold text-on-surface transition-colors hover:bg-surface-high disabled:opacity-50"
                  >
                    {{ 'action.add' | t }}
                  </button>
                </div>
              </div>
            </cs-card>
```

Add i18n keys after `tickets.detail.tagLimit`:

```typescript
  'tickets.detail.links': { en: 'Related tickets', ar: 'التذاكر المرتبطة' },
  'tickets.detail.links.none': { en: 'No linked tickets.', ar: 'لا توجد تذاكر مرتبطة.' },
  'tickets.detail.links.related': { en: 'Related to', ar: 'مرتبطة بـ' },
  'tickets.detail.links.duplicateOf': { en: 'Duplicate of', ar: 'نسخة مكررة من' },
  'tickets.detail.links.duplicatedBy': { en: 'Duplicated by', ar: 'نسخت بواسطة' },
  'tickets.detail.links.type': { en: 'Link type', ar: 'نوع الربط' },
  'tickets.detail.links.targetPlaceholder': { en: 'Target ticket reference (TKT-000000)', ar: 'مرجع التذكرة المستهدفة (TKT-000000)' },
  'tickets.detail.links.remove': { en: 'Remove link to {0}', ar: 'إزالة الربط بـ {0}' },
```

- [ ] **Step 8: Run the tests**

```bash
cd frontend && npx ng test admin-app --watch=false
```

Expected: PASS.

- [ ] **Step 9: Full frontend regression check**

```bash
cd frontend && npx ng test common --watch=false
cd frontend && npx ng test admin-app --watch=false
cd frontend && npx ng build admin-app
```

Expected: all green, build clean. Paste the actual output before claiming the slice done.

- [ ] **Step 10: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.spec.ts frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat(frontend): related/duplicate links section on ticket detail (AC-925.5)"
```
