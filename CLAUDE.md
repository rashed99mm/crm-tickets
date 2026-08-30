# Project instructions

A .NET 10 + Angular 21 full-stack assessment solution, built spec-first.

**The backend is an adapted platform, not a from-scratch build.** On 2026-08-25 the support platform
reference in `refrence/support-platform` was adopted as the CRM baseline and renamed into this domain -
see [ADR-0009](docs/adr/0009-adopt-the-support-platform-as-the-crm-baseline.md) and
`docs/superpowers/specs/2026-08-25-crm-platform-baseline-design.md`. It arrives with Auth, Users,
Contents (the knowledge base), Notifications, PlatformSettings and ExternalApiConfigurations working.
**The ticket workflow is the remaining gap** and is what the brief actually asks for.

## Stack

- **Backend:** .NET 10, C#, Clean Architecture across four layers / eight projects, two API hosts
- **Frontend:** Angular 21 standalone components with signals
- **Testing:** xUnit + `WebApplicationFactory` (backend), Vitest/Karma + Playwright (frontend)

## Structure and the dependency rule

```
backend/
  CustomerSupport.slnx
  src/
    CustomerSupport.Domain/            entities, value objects, events, specifications. Depends on NOTHING.
    CustomerSupport.Application/       CQRS features via MediatR, contracts, behaviors. Domain only.
    CustomerSupport.Infrastructure/    EF Core, Identity, messaging, jobs, localization.
    CustomerSupport.Shared.Contracts/  message contracts shared with external consumers.
    CustomerSupport.Api.Shared/        composition core both hosts share: extensions, middleware,
                                       hubs, configuration (ADR-0008).
    CustomerSupport.InternalApi/       staff host. Full surface. Seeds on start.
    CustomerSupport.ExternalApi/       customer-facing host. Narrow, read-only, anonymous, no seeding.
    CustomerSupport.Migrator/          schema tool.
  tests/
    CustomerSupport.Tests/             the inherited suite
frontend/                              Angular 21 workspace (common lib, admin-app, portal-app)
```

The backend was moved into its own `backend/` folder (mirroring `frontend/`) on 2026-08-25, and the
unused, orphaned `CustomerSupport.AdminApi` project (superseded by `InternalApi`/`ExternalApi` per
ADR-0008) was deleted. All backend commands below run from inside `backend/`.

**The dependency rule is the one invariant that must not bend.** Dependencies point inward
only. `Domain` referencing EF Core, or `Application` referencing `Infrastructure`, is a defect
regardless of how convenient it is at the time — it is the single thing an assessor can check
mechanically, and the whole architecture claim rests on it.

Enforce it in project files, not by discipline: `Domain.csproj` has no `ProjectReference` and no
persistence package.

## Commands

Fill these in as each piece is scaffolded. **Do not invent commands for projects that do not
exist** — an instruction file listing commands that fail is worse than one listing none.

| Task | Command |
|---|---|
| Build backend | `cd backend && dotnet build CustomerSupport.slnx` |
| Test backend | `cd backend && dotnet test CustomerSupport.slnx` |
| Run internal API | `dotnet run --project backend/src/CustomerSupport.InternalApi --urls http://localhost:5074` |
| Run external API | `dotnet run --project backend/src/CustomerSupport.ExternalApi --urls http://localhost:5095` |
| Add migration | `dotnet ef migrations add <Name> --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi` |
| Apply migrations | `dotnet ef database update --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi -- "<connection string>"` |
| Frontend dev server | `cd frontend && npx ng serve admin-app` |
| Frontend tests | `cd frontend && npx ng test common --watch=false` (also `admin-app`, `portal-app`) |
| Frontend build | `cd frontend && npx ng build admin-app` |
| E2E | `cd frontend && npx playwright test` |

### Both hosts need two settings, or every request returns 500

This cost a debugging session once, so it is written down rather than rediscovered:

```
ConnectionStrings__DefaultConnection='Server=(localdb)\MSSQLLocalDB;Database=CustomerSupportCrm;Trusted_Connection=True;TrustServerCertificate=True'
Jwt__Key='<any sufficiently long key>'
```

Without `Jwt:Key`, `AddPlatformAuthentication` throws and the exception middleware - which sits
first in the pipeline - converts it into the standard envelope for **every** request, including
`/openapi/v1.json`. It looks exactly like an infrastructure outage and is not one.

Logging reads its configuration from `appsettings`, which has no console sink. To see an actual
exception, override it: `Serilog__Using__0=Serilog.Sinks.Console Serilog__WriteTo__0__Name=Console`.

Seeded administrator: configure credentials through the seed settings or environment variables; do
not commit a real password. The checked-in settings contain placeholders only.

## The SDD gate

Work moves in one direction. Each stage has an artifact; the next stage does not start until it
exists.

1. **Brief** → `docs/assessment/brief.md`, verbatim.
2. **Spec** → `superpowers:brainstorming` writes `docs/superpowers/specs/`. Must carry
   assumptions and numbered `AC-n` acceptance criteria. **Requires explicit approval.**

Then, **once per feature** (`FEAT-nn` in `docs/requirements/delivery-plan.md`) — not once per
layer, and not once per slice:

3. **Backend plan** → `superpowers:writing-plans` writes `docs/superpowers/plans/`. Every task
   names the `AC-n` it satisfies, and is grounded in the real files it touches — cited paths/lines
   and actual code, not a description of what the code should do
   (`.claude/skills/sdd-workflow/SKILL.md#tasks-are-execution-plans-not-descriptions`).
4. **Implement backend** → `superpowers:test-driven-development`. Failing test first, always.
5. **Frontend plan** → `superpowers:writing-plans` again, for **the same feature**, over the
   frontend `AC-n` its stories cite. Written as soon as step 4 completes. Same rule: real code, real
   file citations.
6. **Implement frontend** → `superpowers:test-driven-development`.
7. **Review** → `superpowers:requesting-code-review` before merge.
8. **Verify** → `superpowers:verification-before-completion` before any completion claim.
9. **Ship** → feature-complete commit, then the next feature.

**Never write implementation code before an approved spec exists.** The temptation is strongest
on tasks that look small, and a "quick" endpoint written ahead of its spec is the exact failure
the first rubric criterion is designed to catch. If a task genuinely seems too small to specify,
say so and ask — do not decide unilaterally.

**Never move to the next feature's backend while this feature's frontend is unwritten.** Step 5
follows step 4 immediately. Backend-then-all-the-screens-later is the layered plan this project
deliberately replaced: it defers every contract mistake to the point where fixing it is most
expensive. The full rule, with its definition of shipped and its recorded exceptions, is in
`.claude/skills/sdd-workflow/SKILL.md`.

A feature is **shipped** when both layers are implemented, every cited `AC-n` has a test naming
it, the suite has been *run* with its output pasted, the build is clean under warnings-as-errors,
and the story files' status reflects what was executed. An endpoint no screen consumes has
shipped nothing.

Some features legitimately have one layer — infrastructure with no user surface, or a capability
whose UI the spec places in another feature's screen. Those are listed in the delivery plan with
a reason. An unrecorded missing layer is indistinguishable from a forgotten one.

## AI usage and verification

This is where agentic work loses marks. The rules are absolute:

- **Never claim a test passes without having run it.** Paste the actual output. "Should pass"
  and "tests pass" are different sentences and only one of them is evidence.
- **Never report work complete on code that has not been executed.** A clean build is not a
  passing test; a passing test is not a working feature.
- **Report failures as failures.** A red test, a skipped step, or an unimplemented branch gets
  stated plainly. Burying it costs the "Technical Understanding & Ownership" criterion outright,
  and it will be found.
- **Review generated code before committing it.** Read every line. Code that cannot be
  explained cannot be defended in the review, and must not be committed.
- **Surface assumptions instead of silently choosing.** When the brief is ambiguous, record it in
  `brief.md` under Assumptions and flag it. Quiet guesses become wrong requirements.
- **Do not fabricate command output, file contents, or version numbers.** Read the file. Run the
  command.

## Git conventions

- Branch: `feat/<slug>`, `fix/<slug>`, `docs/<slug>`, `chore/<slug>`
- Conventional commits: `feat:`, `fix:`, `test:`, `docs:`, `refactor:`, `chore:`
- One logical change per commit. The history is graded; a single "implement everything" commit
  throws away that evidence.
- Commit the spec and plan *before* the code that implements them, so timestamps prove the order.

## Decisions

Non-obvious decisions get an ADR in `docs/adr/` using `template.md`, written when the decision
is made. See `docs/adr/0001-record-architecture-decisions.md` for what counts as non-obvious.

## Traceability

`docs/assessment/rubric-traceability.md` maps each graded criterion to its evidencing artifact.
Update a row's status only after the artifact exists and has been checked.
