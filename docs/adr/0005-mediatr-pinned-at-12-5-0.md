# ADR 0005 — Pin MediatR at 12.5.0 for licensing

- **Status:** Accepted
- **Date:** 2026-08-24

## Context

The project uses CQRS with a request/handler pipeline, and needs pipeline behaviors for validation
and logging. MediatR is the default choice in the .NET ecosystem for this.

MediatR changed its licence. Verified against nuget.org on 2026-08-24:

- `MediatR 12.5.0` — `<license type="expression">Apache-2.0</license>`
- `MediatR 14.2.0` (current) — `<license type="file">LICENSE</license>`, project URL `mediatr.io`

The transition happened at 13.0.0. The same pattern applies to other packages by the same author,
and `FluentAssertions` did the same thing at 8.0 (Xceed). `FluentValidation 12.1.1` was checked
and **is** still Apache-2.0.

## Decision

Pin `MediatR` at exactly **12.5.0**, the last Apache-2.0 release. Pin `FluentAssertions` at
**7.2.0** for the same reason.

Both pins carry a comment in `Directory.Packages.props` (or the `.csproj`) stating *why* the
version is held back, because an un-annotated old version reads as neglect and the next person to
run `dotnet outdated` will helpfully upgrade it into a licence change.

## Alternatives considered

| Option | Why it lost |
|---|---|
| **A hand-rolled dispatcher** — the recommended option | Roughly 40 lines over `IServiceProvider`, no licence question at all, and every line explainable in a review — which is worth marks on the ownership criterion. It lost on preference for the standard library, which is a fair call: MediatR's pipeline semantics are well understood and not worth reimplementing under time pressure. |
| **MediatR 14.2.0 under the commercial licence** | Free below a revenue threshold, so possibly usable — but that is a legal determination about an employer, not a technical choice a developer should make silently in a `.csproj`. Avoided rather than assessed. |
| **Wolverine or another free alternative** | Capable, but a heavier framework with its own conventions, and learning it competes directly with the two to three days available. |

## Consequences

- No licence obligation, and no cost.
- **Deliberately behind two major versions.** Expect to be asked why, which is exactly why this
  record exists. Without it the pin looks like an oversight.
- No security patches or fixes from 13.x or 14.x. Acceptable for an assessment; for a long-lived
  product this becomes a reason to revisit — either accept the commercial terms or move off
  MediatR, and that decision gets its own ADR.
- Any future dependency audit must check licence metadata, not just version currency. Two of this
  project's nine packages are pinned for licence reasons, and a naive "update everything" run
  would silently reintroduce both.
