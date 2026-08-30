# Definition of Done

**This file replaces fifteen "stories" that were never stories** — `US-101`…`US-111`, `US-113`…
`US-115`, `US-122`…`US-124`. They described how the system must behave *everywhere*, which is not
something a user asks for and not something you can demo. Written as stories they consumed 45 points,
each needed its own status row and test-case table, and the platform adoption invalidated all of them
at once.

Here they are one checklist, maintained in one place, applied to **every** story. They are not
estimated and not counted, because they are not optional work — they are what "done" means.

---

## Every story

- [ ] The capability works **end to end in a browser**, not just through an API client.
- [ ] Every acceptance criterion has a test that **names it**, and the suite has been **run with its
      output pasted**. "Should pass" is not evidence.
- [ ] Build clean — 0 errors, and no new warnings.
- [ ] Reviewed line by line before commit. Code that cannot be explained is not committed.

## The API contract

- [ ] Every response — success and failure — is the envelope: `{ isSuccess, data, error, traceId }`.
- [ ] Every failure carries a stable `code` and **both** `messageAr` and `messageEn`. A code with no
      message is a defect. *(Enforced by `EveryErrorCode_HasABilingualMessage`.)*
- [ ] Status codes mean what they say: **400** malformed · **401** unauthenticated · **403** not
      permitted · **404** absent · **409** well-formed but the state is wrong.
- [ ] Validation failures are **keyed to the field** so a form can bind them to the control that
      caused them.
- [ ] Dates on the wire are ISO 8601 with an explicit `Z`. Properties are `camelCase`.

## Security

- [ ] Every endpoint requires a session unless a criterion says otherwise.
- [ ] **The actor comes from the token, never from the payload.** No request body names its own
      author, uploader or actor.
- [ ] Role checks sit on the endpoint. **Per-record checks sit in the handler** — only the handler
      has loaded the record and can see who owns it.
- [ ] No response body contains a stack trace, SQL text, connection string or credential.
- [ ] Nothing is hard-deleted. Deletes are soft, and history is append-only.

## The frontend

- [ ] Loading, empty and error are **three visually distinct states** on every data view.
      `catchError(() => of([]))` renders an outage as "no results" and is forbidden.
- [ ] Server field errors land on the control named by their field, not in a banner.
- [ ] No user-facing string is hardcoded; text resolves through the i18n mechanism.
- [ ] No physical-direction CSS (`ml-`, `text-left`, `border-l`). Logical properties only, so Arabic
      mirrors correctly. *(Enforced by `rtl-safety.spec.ts`.)*

## Data

- [ ] The dependency rule holds: `Domain` references nothing, `Application` references only `Domain`.
- [ ] Integration tests run against **real SQL Server**. `UseInMemoryDatabase` is banned — it honours
      neither filtered unique indexes nor `rowversion`, so it reports criteria as passing while the
      real database rejects the same requests.
- [ ] Concurrent edits to the same record are refused, not silently merged.

---

## Known gaps against this charter

Recorded because an unrecorded gap is indistinguishable from a forgotten one.

| Gap | Where |
|---|---|
| `AC-66` names `ERRnnn` codes; the platform emits named codes | [ADR-0013](../../adr/0013-named-error-codes-over-ac66-numbering.md) — accepted, spec amendment proposed |
| No `X-Trace-Id` **header** — the id is in the body only | `FEAT-09` record |
| Arabic strings are **developer placeholders**, not reviewed copy | `PA-7`; the mechanism is what `MVP-13` delivers, not the translation |
| The envelope sweep covers parameterless `GET` routes only | `FEAT-09` record |
| An access token issued **before** deactivation stays valid up to 60 min; sign-in and refresh are both closed, so the window cannot be extended | `MVP-02` observation |
| `ACCOUNT_DEACTIVATED` reveals that an account exists — enumeration, and awkward beside MVP-01 criterion 2 | `MVP-02` observation |
| A sub-policy password answers `INTERNAL_ERROR` with Identity text, not a field-keyed 400 — no `CreateUserCommand` validator exists | `MVP-02` observation |
| `AC-23` returns **400, not 413**, above ~11 MB — the outer `RequestSizeLimit` cuts the request off before the handler. Sound defence in depth; not the criterion's literal text, and no test covers that boundary | `MVP-06`, found by live testing |
