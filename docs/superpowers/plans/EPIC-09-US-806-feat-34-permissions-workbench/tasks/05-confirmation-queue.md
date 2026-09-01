# Task 05 — Confirmation queue and `details` (US-807, AC-807.1, AC-807.4)

**Files:**
- Modify: `frontend/projects/common/src/lib/ui/confirmation.service.ts` (whole file — 40 lines today)
- Test: `frontend/projects/common/src/lib/ui/confirmation.service.spec.ts` (modify — one test today)

**Interfaces:**
- Consumes: nothing new. `signal`, `computed`, `Subject` from what the file already imports.
- Produces (Tasks 06, 07, 09, 11 rely on these exact names):
  - `ConfirmationRequest` gains `readonly details?: readonly string[]`
  - `ConfirmationService.current: Signal<ConfirmationRequest | null>` — unchanged name, now the head
    of the queue
  - `ConfirmationService.confirm(request: Omit<ConfirmationRequest, 'id'>): Observable<boolean>` —
    unchanged signature
  - `ConfirmationService.resolve(accepted: boolean): void` — unchanged signature
  - `ConfirmationService.pendingCount: Signal<number>` — new, used only by tests and diagnostics

**The bug being fixed.** `confirmation.service.ts:24-28`:

```ts
  confirm(request: Omit<ConfirmationRequest, 'id'>): Observable<boolean> {
    const result = new Subject<boolean>();
    this.pending.set({ id: this.nextId++, ...request, result });   // ← displaces without resolving
    return result.asObservable();
  }
```

A second `confirm()` while one is pending overwrites `pending`. The displaced request's `Subject`
never emits and never completes, so its caller's `subscribe` callback never runs — the caller waits
forever, and any `finally`-style cleanup it owns never happens. With one caller in the codebase
(`kb-admin.component.ts:335`) this is unreachable. `AC-806.19`'s navigation guard can fire while
`AC-806.12`'s save dialog is open, which makes it reachable.

FIFO rather than "newest wins": a save dialog the user is reading must not be replaced under their
cursor by a navigation prompt, and the navigation prompt must still be answered.

## Steps

- [ ] **Step 1: Write the failing tests**

Replace the body of `confirmation.service.spec.ts`, keeping the existing test as the first case:

```ts
import { TestBed } from '@angular/core/testing';
import { ConfirmationService } from './confirmation.service';

describe('ConfirmationService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [ConfirmationService] });
  });

  it('exposes a pending request and resolves the caller observable', () => {
    const service = TestBed.inject(ConfirmationService);
    let result: boolean | null = null;

    service
      .confirm({ title: 'Delete item', message: 'Delete this item?', danger: true })
      .subscribe((accepted) => {
        result = accepted;
      });

    expect(service.current()?.title).toBe('Delete item');

    service.resolve(true);

    expect(result).toBe(true);
    expect(service.current()).toBeNull();
  });

  it('AC807_1_QueuesASecondRequestInsteadOfDroppingIt: both callers receive a result', () => {
    const service = TestBed.inject(ConfirmationService);
    const results: (boolean | null)[] = [null, null];
    const completed = [false, false];

    service.confirm({ title: 'First', message: 'First?' }).subscribe({
      next: (accepted) => (results[0] = accepted),
      complete: () => (completed[0] = true),
    });
    service.confirm({ title: 'Second', message: 'Second?' }).subscribe({
      next: (accepted) => (results[1] = accepted),
      complete: () => (completed[1] = true),
    });

    // The first request stays on screen — a dialog the user is reading is never replaced.
    expect(service.current()?.title).toBe('First');
    expect(service.pendingCount()).toBe(2);

    service.resolve(true);

    expect(results[0]).toBe(true);
    expect(completed[0]).toBe(true);
    // The second is now current, and crucially has NOT been resolved yet.
    expect(service.current()?.title).toBe('Second');
    expect(results[1]).toBeNull();

    service.resolve(false);

    expect(results[1]).toBe(false);
    expect(completed[1]).toBe(true);
    expect(service.current()).toBeNull();
    expect(service.pendingCount()).toBe(0);
  });

  it('AC807_1_ResolveWithAnEmptyQueueIsANoOp: does not throw', () => {
    const service = TestBed.inject(ConfirmationService);

    expect(() => service.resolve(true)).not.toThrow();
    expect(service.current()).toBeNull();
  });

  it('AC807_1_ARequestQueuedFromAResolveCallbackLandsBehindTheQueue: no reentrancy loss', () => {
    const service = TestBed.inject(ConfirmationService);
    const seen: string[] = [];

    // A caller that opens a follow-up dialog the moment its own resolves — exactly what the
    // permissions screen does when a save failure prompts a reload.
    service.confirm({ title: 'First', message: 'First?' }).subscribe(() => {
      seen.push('first');
      service.confirm({ title: 'Third', message: 'Third?' }).subscribe(() => seen.push('third'));
    });
    service.confirm({ title: 'Second', message: 'Second?' }).subscribe(() => seen.push('second'));

    service.resolve(true);
    expect(service.current()?.title).toBe('Second');

    service.resolve(true);
    expect(service.current()?.title).toBe('Third');

    service.resolve(true);
    expect(seen).toEqual(['first', 'second', 'third']);
    expect(service.current()).toBeNull();
  });

  it('AC807_4_CarriesDetails: details travel with the request', () => {
    const service = TestBed.inject(ConfirmationService);

    service
      .confirm({
        title: 'Apply changes',
        message: '2 changes',
        details: ['Grant ticket.close → Agent', 'Revoke report.export → Supervisor'],
      })
      .subscribe();

    expect(service.current()?.details).toEqual([
      'Grant ticket.close → Agent',
      'Revoke report.export → Supervisor',
    ]);
  });
});
```

The reentrancy test is the one that dictates the implementation order inside `resolve()`: the queue
must be updated **before** the subject emits, or a `confirm()` called from the subscriber's callback
gets dequeued along with the request that triggered it.

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd frontend && npx ng test common --watch=false --include='**/confirmation.service.spec.ts'
```

Expected: the four new tests fail — `service.pendingCount is not a function`, and the second
caller's result never arrives. The pre-existing first test must still pass.

- [ ] **Step 3: Rewrite the service**

Replace the whole of `confirmation.service.ts`:

```ts
import { Injectable, computed, signal } from '@angular/core';
import { Observable, Subject } from 'rxjs';

export interface ConfirmationRequest {
  readonly id: number;
  readonly title: string;
  readonly message: string;
  /**
   * Optional lines rendered as a list under the message — used by the permission workbench to show
   * exactly which grants and revokes a save will apply (AC-806.12, AC-807.4). Kept as plain strings
   * rather than a structured type: the dialog's job is to display them, not to interpret them.
   */
  readonly details?: readonly string[];
  readonly confirmText?: string;
  readonly cancelText?: string;
  readonly danger?: boolean;
}

interface PendingConfirmation extends ConfirmationRequest {
  readonly result: Subject<boolean>;
}

/**
 * One dialog at a time, FIFO — never one dialog *instead of* another.
 *
 * The previous implementation held a single `pending` signal and overwrote it, so a second
 * `confirm()` silently discarded the first request's `Subject` without emitting or completing it,
 * leaving that caller waiting forever (AC-807.1). Two prompts can now genuinely overlap: the
 * permissions screen's save dialog and its leave-the-page dialog.
 *
 * FIFO, not newest-wins: replacing a dialog under the user's cursor answers a question they were
 * not reading.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmationService {
  private nextId = 1;
  private readonly queue = signal<readonly PendingConfirmation[]>([]);

  readonly current = computed<ConfirmationRequest | null>(() => this.queue()[0] ?? null);
  readonly pendingCount = computed(() => this.queue().length);

  confirm(request: Omit<ConfirmationRequest, 'id'>): Observable<boolean> {
    const result = new Subject<boolean>();
    this.queue.update((queue) => [...queue, { id: this.nextId++, ...request, result }]);
    return result.asObservable();
  }

  resolve(accepted: boolean): void {
    const [head, ...rest] = this.queue();
    if (!head) {
      return;
    }

    // Dequeue BEFORE emitting. A subscriber commonly opens a follow-up dialog from inside its own
    // callback; if the emit came first, that new request would be dropped by this same update.
    this.queue.set(rest);
    head.result.next(accepted);
    head.result.complete();
  }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
cd frontend && npx ng test common --watch=false --include='**/confirmation.service.spec.ts'
```

Expected: PASS, 5 tests. Paste the output below.

- [ ] **Step 5: Run the whole `common` suite**

```bash
cd frontend && npx ng test common --watch=false
```

Expected: no regressions. `current()` kept its name and its `null`-when-empty contract, so
`confirmation-host.component.html`'s `@if (confirmations.current(); as request)` is unaffected.

- [ ] **Step 6: Commit**

```bash
git add frontend/projects/common/src/lib/ui/confirmation.service.ts \
        frontend/projects/common/src/lib/ui/confirmation.service.spec.ts
git commit -m "fix: queue confirmation requests instead of dropping the displaced caller (AC-807.1, AC-807.4)"
```

## Criteria covered

`AC-807.1` in full. `AC-807.4`'s service half — the rendering half is Task 06.

## Test evidence

Implemented 2026-09-01, in the same run as Task 06 (both spec files run together):

```
npx ng test common --watch=false --include='**/confirmation*.spec.ts'
Test Files  2 passed (2)
     Tests  12 passed (12)
```

Full `common` suite also run (`npx ng test common --watch=false`): 223 passed, 4 failed — all four
failures trace to one already-committed, unmodified file
(`portal-app/kb-list.component.html:57`, a physical `-right-10` utility), confirmed via `git status`
to be untouched by this feature and pre-existing on the branch. No regression from this task's
changes.

## Deviations from the plan

None. Implemented exactly as specified.
