# MVP-06 — Customer attachments · **frontend** implementation plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Date:** 2026-08-26
**Spec:** [`../../specs/EPIC-02-US-001-customer-attachments.md`](../../specs/EPIC-02-US-001-customer-attachments.md)
**Criteria:** `AC-83`, `AC-84`, `AC-85`
**Depends on:** the customer detail screen from
[the customer-workspace frontend plan](../EPIC-02-US-001-mvp-customer-workspace-frontend/implementation-plan.md).
Attachments sit beside notes on it.

## Code plan

### T1 — API surface

**Edit:** `frontend/projects/common/src/lib/customers/customer.api.ts`

```ts
export interface CustomerAttachment {
  id: string; originalFileName: string; contentType: string;
  sizeBytes: number; uploadedByName: string; createdAt: string;
}

/** Mirrors the server's limits so the client can refuse early (AC-84). The SERVER refuses
 *  independently — this is a courtesy that saves a 10 MB round trip, never the control. */
export const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024;
export const ALLOWED_ATTACHMENT_TYPES = [
  'image/png', 'image/jpeg', 'image/gif', 'application/pdf', 'text/plain',
] as const;

listAttachments(customerId: string, page?, pageSize?): Observable<AttachmentPage>
uploadAttachment(customerId: string, file: File): Observable<{ id: string }>  // FormData
downloadUrl(customerId: string, attachmentId: string): string
removeAttachment(customerId: string, attachmentId: string): Observable<unknown>
```

`uploadAttachment` builds `FormData` and **must not set `Content-Type`** — the browser sets the
multipart boundary, and overriding it produces a request the server cannot parse.

### T2 — The attachments component

**New:** `admin-app/src/app/features/customers/customer-attachments.component.{ts,html,spec.ts}`

A sibling of `customer-notes`, so the detail screen composes two independent children and neither
can break the other.

- `AsyncState<AttachmentPage>`; the usual three distinct states.
- **`AC-83`** — list original filename, a human-readable size (`formatBytes`), and type.
- **`AC-84`** — on file selection, check size and type **before** issuing the request; refuse locally with a
  message naming which rule failed. Then upload, and on success re-read the list so the file appears
  without a page reload.
- Show upload progress or at minimum a busy state — a 10 MB upload with no feedback reads as a hang.
- **`AC-85`** — a download link per row, and a remove action behind a confirmation that re-reads on
  success.

### T3 — Download needs the token

`downloadUrl()` returns a plain URL, but the endpoint requires a session and a plain `<a href>`
carries no `Authorization` header.

**Fetch the blob through `HttpClient`** (which the auth interceptor decorates), then
`URL.createObjectURL` and click a synthetic anchor, revoking the object URL afterwards.

A plain link would 401 and look like a broken button. Recorded here because "just link to it" is the
obvious wrong answer.

### T4 — Wire into the detail screen

**Edit:** `customer-detail.component.html` — `<admin-customer-attachments [customerId]="id()" />`
beside the notes.

## Tests

| Test | Criterion |
|---|---|
| `AC83: lists attachments with name, size and type` | `AC-83` |
| `AC83: a failed load renders the error state, not an empty list` | `AC-83` |
| `AC84: a file over the size limit is refused without a request` | `AC-84` |
| `AC84: a disallowed type is refused without a request` | `AC-84` |
| `AC84: a valid file uploads as multipart and the list re-reads` | `AC-84` |
| `AC85: removing an attachment re-reads the list` | `AC-85` |

The two "refused without a request" tests assert `http.expectNone(...)` — the point is that the
client refuses **before** spending the upload, not that it eventually shows an error.

## Constraints

Logical-direction utilities only (`rtl-safety.spec.ts` fails the build otherwise). Plain string
literals for now; `MVP-13` converts them. **Do not touch `backend/`.**

## Definition of done

`AC-83`…`AC-85` each covered by a test naming it · both `ng test` projects green with output pasted ·
`ng build admin-app` clean · task records in `tasks/`.
