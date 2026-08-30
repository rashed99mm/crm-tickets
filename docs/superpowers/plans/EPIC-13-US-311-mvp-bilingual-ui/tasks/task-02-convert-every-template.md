# Task 2 — Convert every template

| Field | Value |
|---|---|
| Plan | [`../implementation-plan.md`](../implementation-plan.md) — T3 |
| Story | `MVP-13` |
| Criteria | `AC-63` |
| Status | `done` — all 15 templates converted |
| Commit | uncommitted — working tree |

## Files

Templates (`.html`):

- `admin-app/.../layout/shell.component.ts` (inline — see task 4)
- `admin-app/.../features/auth/login.component.ts` (inline)
- `admin-app/.../features/errors/forbidden.component.ts` (inline)
- `admin-app/.../features/dashboard/dashboard.component.html`
- `admin-app/.../features/tickets/ticket-queue.component.html`
- `admin-app/.../features/tickets/ticket-create.component.html`
- `admin-app/.../features/tickets/ticket-detail.component.html`
- `admin-app/.../features/customers/customer-list.component.html`
- `admin-app/.../features/customers/customer-create.component.html`
- `admin-app/.../features/customers/customer-detail.component.html`
- `admin-app/.../features/customers/customer-notes.component.html`
- `admin-app/.../features/customers/customer-attachments.component.html`
- `admin-app/.../features/users/users.component.html`
- `admin-app/.../features/account/change-password.component.html`
- `portal-app/.../layout/shell.component.ts` (inline)

Library components:

- `common/.../ui/error-state.component.ts` — the retry label
- `common/.../ui/loading-state.component.ts` — the default label
- `common/.../ui/input-field.component.ts` — `errorText()`

TypeScript that owned user-facing strings:

- `ticket-queue.component.ts` — `emptyMessage()`
- `customer-list.component.ts` — `emptyMessage()`
- `customer-attachments.component.ts` — `limitHint`, `refuse()`

## Test evidence

- `npx ng test common --watch=false` — **80 passed, 0 failed**
- `npx ng test admin-app --watch=false` — **115 passed, 0 failed** (113 before)
- `npx ng test portal-app --watch=false` — **3 passed, 0 failed**
- `npx ng build admin-app` — clean

No existing assertion changed. Every English half in the dictionary is byte-identical to the literal
it replaced, which is why 113 tests that assert on visible English text still pass unedited — and
that is the evidence that this was a mechanical extraction and not a rewrite.

## Three things that were not in a template at all

`AC-63` says "no string is hardcoded in a template". Taken literally that would have left three
user-facing strings in TypeScript, where the sweep in task 3 cannot see them — the worst possible
place for them:

| Where | Was |
|---|---|
| `CsInputField.errorText()` | `'This field is required'`, `'Enter a valid email address'`, and two interpolated length messages |
| `CsLoadingState.label` | defaulted to `input('Loading')` — an English literal baked into the library |
| `CustomerAttachments.refuse()` | `'... is 12.4 MB. The limit is 10 MB.'` and the wrong-type refusal |

All three now go through `t()`. `limitHint` became a `computed` (the template calls `limitHint()`),
because it must re-evaluate on switch.

## What stayed English on purpose

**Domain values the server sends as bare enum names** — `New`, `Open`, `Pending`, `Resolved`,
`Closed`, `Low`/`Normal`/`High`/`Urgent`, and history `changeType` values. They render through
`{{ ticket.status }}` and `<cs-badge [value]>`, so they are *data*, not UI text: the frontend
receives the identifier, not a bilingual message.

Translating them client-side would mean the frontend keeping a second copy of a domain vocabulary the
backend owns, and silently rendering an unknown status as blank the first time the server adds one
(`CsBadge` has a test for exactly that case). The honest fix is a backend one — send status and
priority as `LocalizedMessage`, as every other message already is — and it is **not** in this
story's scope. Recorded here rather than left to be noticed.
