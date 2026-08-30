# ADR 0002 — Use four-project Clean Architecture

- **Status:** Accepted, amended by [ADR-0008](0008-two-api-hosts-shared-composition-core.md) and [ADR-0009](0009-adopt-the-support-platform-as-the-crm-baseline.md) · amended by [ADR-0008](0008-two-api-hosts-shared-composition-core.md) (the single `Api` host became `Api.Shared` + two thin hosts; the layering and dependency rule here are unchanged)
- **Date:** 2026-08-24

## Context

The S1 slice needs a backend structure before any code is written. The assessment grades
"separation of concerns" explicitly, and whatever structure is chosen has to be defensible under
questioning rather than adopted by default.

The domain has one genuinely interesting rule — the ticket status machine — plus per-record
authorization. Both need to be unit-testable without a database, because they carry the most
acceptance criteria in the slice (AC-37 to AC-47).

## Decision

Four projects: `Domain`, `Application`, `Infrastructure`, `Api`, with dependencies pointing
inward only. `Domain` has no project references and no persistence packages.

## Alternatives considered

| Option | Why it lost |
|---|---|
| **Vertical slice + Minimal APIs** — feature folders, one handler per endpoint, no layer projects | Genuinely a good fit for a slice this size and less ceremony. It lost on the explicit instruction to use Clean Architecture, and because the layer boundary here is mechanically checkable: an assessor can open `Domain.csproj` and verify the claim in seconds. A vertical-slice layout's discipline is real but invisible. |
| **Layered controllers → services → repositories** | The service layer ends up depending on the persistence layer, so the domain rules cannot be tested without a database. That directly undermines the status-machine tests, which are the strongest part of this slice. |
| **Single project with folders** | Nothing prevents the domain from acquiring an EF dependency, and by the time it has one the architecture claim is untrue. The compiler should enforce the rule, not a reviewer's attention. |

## Consequences

- The status machine and authorization rules are unit-testable with no infrastructure, which is
  what makes their test coverage cheap enough to be thorough.
- More projects, more `.csproj` files, and more indirection than this slice strictly needs. This
  is real overhead and should be acknowledged rather than defended as free.
- `Application` must declare port interfaces for everything it needs (`IFileStore`, `IClock`,
  repositories). That inversion is the cost of the dependency rule and also the thing that makes
  handlers testable.
- Adding a reference from `Application` to `Infrastructure` would silently undo the whole
  decision, so the project files are the enforcement point and are worth reviewing in any PR that
  touches them.

## Amended 2026-08-25

The four *layers* still hold and are still enforced by the dependency rule. The project *count* does
not: there are now eight projects and two API hosts (ADR-0008), and the code inside them came from
the adopted platform rather than being written here (ADR-0009). Read this ADR for why the layering
exists; read those two for what the solution actually contains.
