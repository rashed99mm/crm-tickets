# Non-functional requirements

The authoritative register is the BRD's `NFR-1`–`NFR-22` — concrete targets with slice assignments,
reproduced below with honest status. The rule specification's §16 categories (`NFR-SEC-001`,
`NFR-PERF-001`, …) are satisfied through these rows; the mapping follows the table. Status means
what it always means here: **proven** only where a test or build has actually demonstrated it.

## Register

| ID | Requirement | Target / verification | Slice | Status |
|---|---|---|---|---|
| NFR-1 | List endpoint response time | p95 < 500 ms at 100,000 tickets / 20,000 customers | S1 | Target set; **measurement pending** — no dataset near that volume exists yet |
| NFR-2 | Every collection endpoint paginated, server-enforced max page size | No unbounded list | S1 | Specified (`US-013`, `US-035`), not built |
| NFR-3 | Availability during business hours | 99.5% monthly | S6 | Open — no deployment exists |
| NFR-4 | All traffic encrypted in transit | TLS 1.2 minimum, no plaintext listener | S1 | Open — hosting decision pending (ADR owed) |
| NFR-5 | Passwords only as salted hash, current adaptive algorithm | Never reversible, logged or returned | S1 | Specified (`US-112`, `US-115`), not built |
| NFR-6 | No stack trace / SQL text / connection string in any response body | Single error boundary | S1 | Envelope halves tested (`US-101`, `US-123` criteria); full proof needs real endpoints |
| NFR-7 | Attachments outside web root, streamed only after authorization | No static path serves user content | S1 | Specified (`US-131`, `US-132`), sprint 5 |
| NFR-8 | Upload allowlist + size cap checked before stream consumed | Allowlist, never blocklist | S1 | Specified (`US-008`), sprint 5 |
| NFR-9 | Every state change attributable to actor + UTC timestamp | 100% of changes | S1 | **Proven** — `US-109` auditing tests passing (`FND-23..26`) |
| NFR-10 | Correlation id on every response, matching server log | Support without shipping diagnostics | S1 | Partial — presence tested (`US-103`); log-match unproven |
| NFR-11 | Every system message available in Arabic and English | No monolingual response | S1 | **Proven** — `US-106`/`US-107` tests passing (`FND-14..21`) |
| NFR-12 | No user-facing string hardcoded in a template | Verified by review; S8 adds a file | S1, S8 | Not started — frontend arrives sprint 4 |
| NFR-13 | Layout direction follows active locale | Full RTL correctness at S8 | S1, S8 | Not started |
| NFR-14 | Accessibility WCAG 2.1 AA | Portal and agent application | S3, S8 | Not started |
| NFR-15 | Browser support and responsiveness | Current + previous Chrome/Edge/Firefox/Safari; usable from 360 px | S1, S8 | Devices **defined** (this row answers the rule file's open device question); verification via Playwright later |
| NFR-16 | Wire format | Dates ISO 8601 UTC; JSON camelCase | S1 | Partial — camelCase asserted (`US-124`); date half awaits first dated DTO |
| NFR-17 | Backup and recovery | Daily backup; RPO 24 h, RTO 4 h | S6 | Open — operations |
| NFR-18 | Attachment storage swappable behind a port | No business-logic change to swap storage | S1 | Design decided (`US-008`), implementation sprint 5 |
| NFR-19 | Dependency rule enforced mechanically | Build failure, not a comment | S1 | **Proven** — `US-110` test passing (`FND-29`) |
| NFR-20 | Warnings are build failures | Warnings-as-errors, nullable enabled | S1 | **Proven** — all 96 tests ran on a clean warnings-as-errors build |
| NFR-21 | Multi-branch on a single deployment | Scoping, not database-per-branch (`B4`) | S8 | Scheduled S8 |
| NFR-22 | Reporting load does not degrade operational response | Measured under concurrent load | S6 | Open — reporting epic sprint 13 |

## Mapping from rule specification §16

| Rule-file category | Covered by |
|---|---|
| `NFR-SEC-001` Authorization | NFR-4/5 + the authorization rules in [`security.md`](../architecture/security.md) (`AC-4`, `AC-43`, `AC-45..47`) |
| `NFR-SEC-002` Auditability | NFR-9 + immutable ticket history (`US-121`) |
| `NFR-PERF-001` Performance | NFR-1 (list/search/customer/dashboard/report p95), NFR-2 (pagination), NFR-22 (report isolation) — targets set from expected load, per the category's own instruction |
| `NFR-AVL-001` Availability | NFR-3 + NFR-17 — **answered**, not left open: 99.5% business hours, RPO 24 h / RTO 4 h |
| `NFR-I18N-001` Localization | NFR-11/12/13 — user-selectable language settled (`US-093`, `BR-22`); default language remains assumption `PA-7` |
| `NFR-RESP-001` Responsive interface | NFR-14/15 — supported browsers and the 360 px floor are **defined** |
| `NFR-DATA-001` Data protection | NFR-7/8/17/18 cover handling, backup, storage; **retention and deletion periods remain genuinely unset** — no BRD row defines them; flagged in [`security.md`](../architecture/security.md) rather than invented |
| `NFR-INT-001` Integration resilience | Owed per-adapter from sprint 9 — see [`integrations.md`](../architecture/integrations.md) |

## Honest summary

Of 22 rows: **4 proven by executed tests/build**, 3 partial, 9 specified-not-built, 6 open
(deployment/operations/slices not reached). Two genuine gaps exist beyond the register: customer
data retention/deletion policy, and rate limiting/lockout/MFA (out of S1 scope by decision, stated
in security.md). Neither is quietly absorbed into a story; both stay visible until someone with
authority closes them.
