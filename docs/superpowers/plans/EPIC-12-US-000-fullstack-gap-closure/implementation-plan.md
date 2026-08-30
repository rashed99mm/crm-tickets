# Full Stack Gap Closure Execution Plan

**Spec:** [`../../specs/EPIC-12-US-000-fullstack-gap-closure-sdd.md`](../../specs/EPIC-12-US-000-fullstack-gap-closure-sdd.md)  
**Status:** In progress  
**Goal:** Finish all CRM UI/UX gap report items to 100% with backend, frontend, tests, Stitch alignment, and evidence.

## Execution Model

Each task is a vertical slice:

1. Read current backend controller/entity and Angular component/API.
2. Write failing tests for the named acceptance criteria.
3. Implement backend domain, migration, commands/queries, controller.
4. Implement frontend API client, route/nav, component, template, translations.
5. Run focused tests, then full affected build/test gates.
6. Paste real command output summary into the task file.
7. Update delivery-plan/rubric only after evidence exists.

## Dependency Order

| Order | Task | Reason |
|---|---|---|
| 01 | Customer 360 completion | Adds data model needed by profile and reporting |
| 02 | Ticket/SLA automation admin | Unlocks routing/escalation/business-hour setup |
| 03 | Communication channel inboxes | Replaces static channel cards and normalizes conversations |
| 04 | Agent workspace | Depends on ticket/channel/task primitives |
| 05 | Knowledge base completion | Uses existing content/version/category model |
| 06 | AI completion | Depends on ticket/chat/KB route stability |
| 07 | Portal account recovery | Independent auth flow, can run in parallel after 01 |
| 08 | Reports and management | Depends on customer/profile/channel/SLA data completeness |
| 09 | Security/admin profile | Depends on user department/profile fields |
| 10 | Integrations | Replaces static integration cards and secret handling |
| 11 | Platform structure/branding | Branch/team/language/org tree and runtime branding |
| 12 | Stitch evidence and dead-control audit | Final cross-screen verification |
| 13 | Ticket lifecycle completion | Surfaces existing lifecycle metadata and escalation ownership |

## Task Index

- [Task 01 - Customer 360 Completion](tasks/task-01-customer-360-completion.md)
- [Task 02 - Ticket SLA Automation Admin](tasks/task-02-ticket-sla-automation-admin.md)
- [Task 03 - Communication Channel Inboxes](tasks/task-03-communication-channel-inboxes.md)
- [Task 04 - Agent Workspace Collaboration](tasks/task-04-agent-workspace-collaboration.md)
- [Task 05 - Knowledge Base Completion](tasks/task-05-knowledge-base-completion.md)
- [Task 06 - AI Experience Completion](tasks/task-06-ai-experience-completion.md)
- [Task 07 - Portal Account Recovery](tasks/task-07-portal-account-recovery.md)
- [Task 08 - Reports Management Completion](tasks/task-08-reports-management-completion.md)
- [Task 09 - Security Admin Profile Completion](tasks/task-09-security-admin-profile-completion.md)
- [Task 10 - Integrations Completion](tasks/task-10-integrations-completion.md)
- [Task 11 - Platform Structure Branding](tasks/task-11-platform-structure-branding.md)
- [Task 12 - Stitch Evidence Dead Control Audit](tasks/task-12-stitch-evidence-dead-control-audit.md)
- [Task 13 - Ticket Lifecycle Completion](tasks/task-13-ticket-lifecycle-completion.md)

## Shared Backend Patterns

Use Clean Architecture boundaries already present in the repo:

```text
Domain/Entities/<Area>/<Entity>.cs
Application/Features/<Area>/Commands/<UseCase>/*
Application/Features/<Area>/Queries/<UseCase>/*
Infrastructure/Persistence/Configurations/<Entity>Configuration.cs
Infrastructure/Migrations/<timestamp>_<Name>.cs
InternalApi/Controllers/<Area>Controller.cs
```

Every new mutation:

```csharp
public sealed record UpdateSomethingCommand(Guid Id, string Name)
    : IRequest<Response<SomethingDto>>;

public sealed class UpdateSomethingCommandValidator : AbstractValidator<UpdateSomethingCommand>
{
    public UpdateSomethingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
    }
}
```

## Shared Frontend Patterns

Use Angular standalone components, signals, typed clients, and `.html` templates:

```ts
readonly state = signal<AsyncState<readonly RowDto[]>>(loading());
readonly rows = computed(() => this.state().status === 'loaded' ? this.state().data : []);

load(): void {
  this.state.set(loading());
  this.api.list().subscribe({
    next: (result) => this.state.set(fromList(result.items)),
    error: (error: unknown) => this.state.set(failed(toApiError(error))),
  });
}
```

Every component must render:

```html
@switch (state().status) {
  @case ('loading') { <cs-loading-state /> }
  @case ('error') { <cs-error-state [error]="state().error" (retry)="load()" /> }
  @case ('empty') { <cs-empty-state [message]="emptyKey | t" /> }
  @default { <!-- real data UI --> }
}
```

## Verification Matrix

| Gate | Command |
|---|---|
| Backend full suite | `dotnet test backend/CustomerSupport.slnx` |
| Common frontend | `cd frontend && npx ng test common --watch=false` |
| Admin frontend | `cd frontend && npx ng test admin-app --watch=false` |
| Portal frontend | `cd frontend && npx ng test portal-app --watch=false` |
| Admin build | `cd frontend && npx ng build admin-app` |
| Portal build | `cd frontend && npx ng build portal-app` |
| E2E/Stitch | `cd frontend && npx playwright test` |

## Risks

- Several gaps touch schema and migrations; split commits by slice.
- API secrets and customer financial data need authorization review.
- Realtime presence/internal chat may require hub contract changes in both API hosts.
- Report exports can be slow; implement streaming/file result and background job only if needed.
- The existing worktree is dirty; do not revert unrelated changes.

## Evidence

- 2026-08-29 slice 01: Implemented durable live-chat session/message entities, internal `/api/chat` endpoints, external anonymous `/api/external/chat` endpoints, and API-backed AI reply suggestions for the admin chat workspace.
- 2026-08-29 slice 02: Wired SLA business-hours and holiday administration into the SLA policies screen using the existing backend endpoints.
- 2026-08-29 slice 03: Surfaced ticket lifecycle metadata in detail view and added escalation-owner handoff from escalated tickets.
- 2026-08-29 slice 04: Wired live-chat realtime push from backend `IRealTimeNotifier` to existing Angular `ChatStore` listener.
- Verified `cd frontend && npx ng build admin-app` passes; warnings remain the existing dashboard unused-import warnings and initial bundle budget warning.
- Verified focused Angular tests:
  - `cd frontend && npx ng test admin-app --watch=false --include=projects/admin-app/src/app/features/chat/chat-session.component.spec.ts` passed 2 tests.
  - `cd frontend && npx ng test common --watch=false --include=projects/common/src/lib/channels/chat.api.spec.ts` passed 5 tests.
- Backend build command `dotnet build backend/src/CustomerSupport.InternalApi/CustomerSupport.InternalApi.csproj` currently fails before `Csc` with `Build FAILED. 0 Warning(s) 0 Error(s)` under local .NET SDK 10.0.302; diagnostic log saved at `backend-build-diag.log`.
