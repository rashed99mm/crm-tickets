# Security

Security decisions and rules for slice S1, with their status. Sources: the ticket-lifecycle spec's
authentication criteria (`AC-1..6`), the foundation spec, BRD rules (`BR-1..23`) and its NFR
register. Nothing here overrides those documents; this view groups them for review.

## Authentication

| Rule | Source | Status |
|---|---|---|
| Staff-only sign-in; session established server-side | `AC-1..3`, `US-112` | Specified, not built |
| Failed sign-in reveals nothing — unknown account and wrong password are indistinguishable, same latency class | `AC-2`, `AC-6`, `US-113` | Specified |
| Passwords stored only as salted hash from a current adaptive algorithm; never reversible, never logged, never returned | `NFR-5`, `US-115` | Specified |
| Credentials never appear in any response or log | `US-115`, `AC-5` | Specified |

## Authorization

Enforced at the domain/API boundary, never only in the UI (principle 17.4):

| Rule | Source | Status |
|---|---|---|
| Role permissions refuse explicitly — a forbidden action is a typed refusal, not a hidden button | `AC-4`, `US-114` | Specified |
| Agents cannot assign tickets; assignment belongs to a **Supervisor**, including reassigning a ticket to themselves | `AC-42..43`, `US-119`, `BR-10` | Specified |
| Status change belongs to the assignee — an agent may change only their own ticket, a supervisor any | `AC-45..47`, `US-120`, `BR-11` | Specified |
| Reopen is an **ordinary status transition**, not a role-restricted action; it is guarded against lost updates by the concurrency token | `AC-40..41`, `US-026`, `BR-11`, `BR-13` | Specified |
| Customer delete is a guard, refused while tickets reference the customer | `AC-15..16`, `US-117`, `BR-7` | Specified |
| Actor attribution comes from the authenticated session, never from payload; payload value ignored | `BR-6`, `US-007`, `AC-19` | Proven pattern at foundation level (`US-109` auditing tests pass) |

> **Corrected 2026-08-25.** Four rows above carried wrong rule citations and two named roles that do
> not exist. Assignment cited `BR-3` (a status-transition rule) instead of `BR-10`; status-change
> ownership cited `BR-7` (the delete guard) instead of `BR-11`; the delete guard itself cited `BR-1`
> (ticket-to-customer cardinality) instead of `BR-7`; and reopen cited `BR-5` (history is
> append-only) while asserting a role restriction that **no business rule and no acceptance
> criterion supports** — `AC-40` describes reopen as an ordinary transition. "Team Lead" and
> "Manager" were never system roles: S1 seeds exactly `Agent` and `Supervisor` (S1 spec `A2`,
> ADR 0003). Note that `Support Agent / Team Lead / Support Manager` in
> `system-overview.md` are **personas** — job titles in `docs/product/03-personas.md` — not
> authorization roles, and are correct as written there.

## Data protection

| Rule | Source | Status |
|---|---|---|
| Attachments stored outside the web root; streamed only after authorising the caller; no static path serves user content | `NFR-7`, `US-131..132` | Specified |
| Uploads restricted by content-type allowlist (never blocklist) and size cap checked before the stream is consumed | `NFR-8`, `US-008` | Specified |
| Client-supplied filename can never influence storage location (`../` traversal, absolute paths, reserved names) | `AC-25`, `US-131` | Specified |
| Attachment storage swappable without touching business logic (port) | `NFR-18` | Design decided; implementation sprint 5 |
| Soft delete everywhere; deletes are guards, not removals — support history is never destroyed | `FND-23..26`, `US-109`, `BR-1` | **Proven** — foundation tests passing |
| Backup/recovery targets (daily backup, RPO 24 h, RTO 4 h) | `NFR-17` | Open — operations concern, slice S6 |
| Customer data retention and deletion policy | — | **Genuine gap** — no BRD row sets retention periods; flagged for the product owner rather than silently assumed |

## Auditability

Every state change carries actor and UTC timestamp (`NFR-9`) — **proven** by the `US-109`
auditing tests (`FND-23..26`). Ticket changes additionally append immutable history rows
(`US-121`, `AC-48..49`), covering the security-sensitive classes `NFR-SEC-002` names for S1 scope:
status, priority and assignment changes. SLA configuration and permission changes enter scope with
their epics (sprints 8 and 12).

## Transport and error hygiene

- TLS 1.2 minimum, no plaintext listener (`NFR-4`) — hosting-level; **open** until the deployment
  ADR exists.
- No response body contains a stack trace, SQL text or connection string (`NFR-6`); failures carry
  stable codes and a `traceId` that matches the server log (`NFR-10`) — correlation without
  disclosure. Envelope parts of this are **tested** (`FND-1..7`); the log-match half of `NFR-10`
  is not yet provable (see coverage, [US-103](../requirements/user-stories/US-103-trace-id-and-timestamp.md)).
- Secrets live outside the repository; configuration is environment-provided. No connection string
  is committed anywhere in this solution.

## Known limits, stated plainly

Rate limiting, account lockout and MFA are **out of S1 scope** — they are not in any spec, story or
BRD row. They belong to Administration/hardening (roadmap sprints 8/12) and must be specified there,
not improvised. If an assessor asks how sign-in survives credential stuffing today, the honest
answer is: it doesn't yet, and the roadmap says when it will.
