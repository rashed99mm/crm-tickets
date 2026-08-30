# CRM Platform Baseline Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Goal:** Adopt the CCE Platform reference as the CRM backend, renamed and split into internal and external hosts, then leave the ticket workflow as the remaining gap (`BASE-11`..`BASE-14`).

**Spec:** `docs/superpowers/specs/EPIC-12-US-000-crm-platform-baseline-design.md` (`BASE-1`..`BASE-14`). ADR-0009 records the decision.

**Approach:** adopt, rename, verify, then extend. The inherited suite (97 passing at adoption) is the proof the rename compiled *and* was correct.

## Global constraints

- No `CCE` identifier, namespace, project or database name survives (word-bounded match, so `SUCCESS`/`ACCESS` untouched).
- Both hosts share one composition core (`Api.Shared`) — never duplicate pipeline wiring.
- XML documentation on for every project.

## Task 1 — Adopt, rename, restructure (`BASE-1`..`BASE-4`)

Archiving, renaming 15 files, flattening the layout, extracting the shared core. These are file/rename operations with no business-logic snippet; the real *code* outcome is the project set described in `EPIC-01-US-101-backend-foundation`. The verification that mattered: build 0/0 and `97/97` inherited tests green after the rename.

## Task 2 — External (customer) host: published articles only (`BASE-5`..`BASE-8`)

**Files:**
- `backend/src/CustomerSupport.ExternalApi/Program.cs` (same core as InternalApi, no seeding)
- `backend/src/CustomerSupport.ExternalApi/Controllers/KnowledgeBaseController.cs`

**Interfaces:** anonymous, read-only, published-only. `Status` filter is applied server-side and is **not** a query parameter — a caller choosing the status would expose drafts.

**Step 1 — Real controller (excerpt)**

```csharp
// backend/src/CustomerSupport.ExternalApi/Controllers/KnowledgeBaseController.cs
[ApiController]
[Route("api/knowledge-base")]
[ApiVersion("1.0")]
[Produces("application/json")]
// NOT [AllowAnonymous] at class level: a class-level bypass cannot be overridden by [Authorize]
// on one action, so each read action carries its own [AllowAnonymous] and Vote carries [Authorize].
public class KnowledgeBaseController : ControllerBase
{
    private const string PublishedStatus = "Published";

    [HttpGet("articles")]
    [AllowAnonymous]
    public async Task<IActionResult> GetArticles(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetContentsQuery
        {
            PageIndex = page, PageSize = pageSize, SearchTerm = searchTerm, Status = PublishedStatus,
        }, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("articles/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetArticle(Guid id, CancellationToken ct = default)
        => this.ToActionResult(await _mediator.Send(new GetContentByIdQuery(id), ct));

    [HttpGet("articles/faq")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFaqArticles(CancellationToken ct = default)
        => this.ToActionResult(await _mediator.Send(new GetFaqContentsQuery(), ct));

    [HttpPost("articles/{id:guid}/vote")]
    [Authorize]                                  // AC-187/188: one vote per user needs an identity
    public async Task<IActionResult> Vote(Guid id, [FromBody] VoteOnContentRequest request, CancellationToken ct = default)
        => this.ToActionResult(await _mediator.Send(new VoteOnContentCommand(id, request.IsHelpful), ct));
}
```

**Step 2 — Run:** `cd backend && dotnet build CustomerSupport.slnx` → 0/0; serve ExternalApi and `curl /api/knowledge-base/articles` → 200 with published-only rows.

**Step 3 — Commit:** `git commit -m "feat(baseline): external host, published-only knowledge base (BASE-5..BASE-8)"`

## Task 3 — Make it run: the missing `Jwt:Key` (`BASE-9`, `BASE-10`)

**Files:** `CLAUDE.md` (run instructions), `backend/src/CustomerSupport.Api.Shared/Extensions/AuthenticationExtensions.cs` (Task 4 of foundation).

Root cause was a missing `Jwt:Key`, not Redis/RabbitMQ (the plan's original guess, recorded as wrong). Proven by overriding Serilog to a console sink:
`Serilog__Using__0=Serilog.Sinks.Console Serilog__WriteTo__0__Name=Console`.
Also delivered: `XmlDocumentationTransformer` so `GenerateDocumentationFile` XML reaches the served OpenAPI (35 ops internal, 2 external).

**Step 1 — Run (from any host dir):**
```
ConnectionStrings__DefaultConnection='Server=(localdb)\MSSQLLocalDB;Database=CustomerSupportCrm;Trusted_Connection=True;TrustServerCertificate=True' `
Jwt__Key='any-long-enough-key' `
dotnet run --project backend/src/CustomerSupport.InternalApi --urls http://localhost:5074
```
Expected: `/openapi/v1.json` returns 200 and carries `<summary>` prose (BASE-10) once both settings are present.

**Step 2 — Commit:** `git commit -m "fix(baseline): supply Jwt:Key so hosts serve instead of 500 (BASE-9)"`

## Task 4 — Re-point the frontend envelope (`BASE-9` cross-cut)

**Files:** `frontend/projects/common/src/lib/api/envelope.interceptor.ts`

The Angular app expected the earlier hand-built envelope; the adopted platform returns `isSuccess`/`error.code`/`messageAr`/`messageEn`. `envelopeInterceptor` is the single place that knows the envelope exists — one file, so this was one file plus tests.

```ts
// frontend/projects/common/src/lib/api/envelope.interceptor.ts
export const envelopeInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    map((event) => {
      if (event instanceof HttpResponse && isApiEnvelope(event.body)) {
        return event.clone({ body: (event.body as ApiEnvelope<unknown>).data });
      }
      return event;
    }),
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && isApiEnvelope(error.error)) {
        const env = error.error as ApiEnvelope<unknown>;
        const fieldErrors = (env.errors ?? []).map((e) => ({
          field: toControlName(e.field), code: e.code, message: e.message,
        }));
        return throwError(() => new ApiError(env.code, env.message, fieldErrors, env.traceId ?? '', error.status));
      }
      return throwError(() => new ApiError('ERR_NETWORK', 'Could not reach the server', [], '', error.status));
    }),
  );
```

**Step 1 — Run:** `cd frontend && npx ng test common --watch=false --filter envelope`
Expected: PASS — enveloped success unwraps to `data`, enveloped failure becomes `ApiError` with lowercased field names, bare 502 still yields a displayable `ApiError`.

**Step 2 — Commit:** `git commit -m "feat(baseline): frontend envelope re-pointed to platform shape (BASE-9)"`

## Task 5 — The ticket workflow gap (`BASE-11`..`BASE-14`)

The reference had no tickets. This gap is closed by the dedicated feature plans, each carrying the actual code: `EPIC-02-US-001-feat-03-customer-records/`, `-feat-04-ticket-capture/` (+`-frontend/`), `-feat-05-ticket-queue/` (+`-frontend/`), and later `EPIC-02-US-016-feat-06-ticket-lifecycle/`, `-feat-07-assignment-authorization/`, `-feat-08-ticket-history/`. Restating that code here would be a second, drifting copy; the remaining job of this task is to record that the gap was closed and where.

## Self-review

Coverage: `BASE-1`–`BASE-4` → Task 1; `BASE-5`–`BASE-8` → Task 2; `BASE-9`–`BASE-10` → Tasks 3–4; `BASE-11`–`BASE-14` → Task 5 (deferred to feature plans).

**Discrepancy found:** the original plan guessed Redis/RabbitMQ were the runtime blocker. Real root cause was the missing `Jwt:Key` (Task 3). The guess cost time and is recorded as such — the honest risk is that a guessed dependency failure can mask the real one.
