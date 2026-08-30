# Full Stack Gap Closure SDD

**Date:** 2026-08-29  
**Status:** Approved for execution planning  
**Source:** Frontend UI/UX Gap Report - Customer Support CRM  
**Goal:** Close every scorecard gap to 100% with backend contracts, Angular implementation, Stitch-aligned UX, tests, and evidence.

## 1. Problem

The CRM has many screens that look close to finished, but the gap report found three different
failure modes:

- **Decorative UI:** buttons/cards are visible but do not call an API or mutate state.
- **Missing frontend over existing backend:** controllers/entities exist, but no admin/agent screen uses them.
- **Missing full-stack capability:** product fields or workflows need domain model, migration, API, SignalR, Angular, tests, and Stitch design alignment.

The fix is not a single refactor. It is a controlled full-stack gap-closure program that converts
every gap into a named, testable work item.

## 2. Non-Negotiable Product Rules

- No placeholder card can claim a capability is configured or working.
- Every visible button must either perform a real action, open a real form/dialog, navigate to an existing route, or be removed.
- Every table/list must show loading, empty, error, and loaded states.
- All staff UI strings use translation keys.
- All new backend endpoints use the existing response envelope, authorization policies, validation, audit logging, and UTC timestamps.
- Stitch mockups remain the visual reference, but operational CRM screens stay dense, scannable, and task-first.
- No secret value is rendered in full. API keys and provider credentials are masked by default.

## 3. Architecture Context

Existing backend surfaces to reuse:

| Capability | Existing context |
|---|---|
| Branches | `BranchesController`, `Organisation/Branch` |
| Teams | `TeamsController`, `Organisation/Team` |
| Business hours/holidays | `BusinessHoursController`, `BusinessHoursCalendar`, `PublicHoliday` |
| External integrations | `ExternalApiConfigurationsController`, `ExternalApiConfiguration` |
| Audit log | `AdminController`, `AuditLog` |
| Knowledge base | `ContentsController`, `ContentCategoriesController`, `ContentVersion` |
| AI ticket assistance | `AiController`, `AiSuggestion`, `AiApi` |
| Live chat | `ChatApi`, `ChatStore`, `MainHub` |
| Agent work artifacts | `TicketTask`, `QuickReply`, `TicketNote` |

Frontend surfaces to extend:

| Area | Files |
|---|---|
| Shell/nav | `frontend/projects/admin-app/src/app/layout/*`, `app.routes.ts` |
| Customers | `features/customers/*`, `common/src/lib/customers/customer.api.ts` |
| Tickets/chat | `features/tickets/*`, `features/chat/*`, `common/src/lib/tickets`, `common/src/lib/channels` |
| Admin/platform | `features/admin/*`, `features/users/*`, `features/organisation/*`, `common/src/lib/admin` |
| Reports | `features/reports/*`, `common/src/lib/reports/report.api.ts` |
| Portal | `frontend/projects/portal-app/src/app/features/**` |

## 4. Stitch Design Contract

Use the Stitch references as screen-level composition inputs:

| Stitch reference | Implementation usage |
|---|---|
| `customer_360_history` | Customer 360 fields, tickets lane, notes, attachments |
| `ticket_detail_chatbot` | Ticket detail, AI rail, message composer, KB citations |
| `ai_powered_agent_workspace` | Agent workspace, tasks, quick replies, collaboration |
| `management_analytics_sla_performance` | Reports, KPI cards, trend panels, exports |
| `security_users_roles` | Users table, per-row action menu, department assignment |
| `security_audit_logs` | Audit filters, CSV export, row detail |
| `system_configuration` | Branding, integrations, routing rules, branches, teams |

Visual rules:

- Use left navigation and dense work areas, not landing-page hero composition.
- Use cards only for repeated records, dialogs, and framed tools.
- Use icons for edit, delete, export, copy, visibility, configure, refresh, send, assign, and AI.
- Avoid hardcoded static metrics; dashboard and insight values come from API state or computed loaded data.
- Make all desktop tables usable on mobile through responsive stacking or horizontal scroll.

## 5. Capability Slices

### Slice A - Customer 360 Completion

Closes: WhatsApp, tags, plan/tier, email verified, manager, MRR, timezone, HQ, customer tickets lane,
note edit/delete, attachment rename.

Backend:

- Extend `Customer` with CRM profile fields.
- Add `CustomerTag` or normalized tag table if tag search/filter is required.
- Add note update/delete commands.
- Add attachment rename command.
- Add `customerId` filter to ticket queue query.

API shape:

```csharp
public sealed record UpdateCustomerProfileRequest(
    string? WhatsAppNumber,
    string? PlanTier,
    Guid? AccountManagerId,
    decimal? MonthlyRecurringRevenue,
    string? TimeZone,
    string? Headquarters,
    IReadOnlyList<string> Tags);
```

```csharp
[HttpGet("{customerId:guid}/tickets")]
public Task<IActionResult> Tickets(Guid customerId, [FromQuery] GetTicketsQuery query, CancellationToken ct)
{
    query.CustomerId = customerId;
    return Send(query, ct);
}
```

Frontend:

- Add editable Customer 360 panel with profile fields.
- Add tickets lane using `TicketApi.list({ customerId })`.
- Add inline note edit/delete.
- Add attachment rename dialog.

Acceptance:

- Customer detail shows real profile values from API.
- Saving profile persists and reloads without placeholders.
- Customer ticket lane filters by customer id and links to ticket detail.
- Notes support create, edit, delete with audit history.
- Attachment rename changes display filename without reuploading binary.

### Slice B - Ticket, SLA, And Automation Admin

Closes: escalation rules UI, auto-assignment rules UI, business hours, holidays, email/SMS alert config.

Backend:

- Introduce `EscalationRule`, `AssignmentRule`, and `NotificationRule` entities if not already present.
- Reuse `BusinessHoursCalendar` and `PublicHoliday`.
- Add rule ordering, enable/disable, validation, and audit.

API shape:

```csharp
public sealed record UpsertAssignmentRuleRequest(
    string Name,
    int Priority,
    string ConditionJson,
    Guid? DepartmentId,
    Guid? TeamId,
    Guid? AgentId,
    bool IsActive);
```

```csharp
[ApiController]
[Route("api/automation/assignment-rules")]
[Authorize(Policy = "Admin")]
public sealed class AssignmentRulesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) =>
        Send(new GetAssignmentRulesQuery(), ct);
}
```

Frontend:

- Replace static routing table in Platform Settings with `AssignmentRulesComponent`.
- Add `EscalationRulesComponent`.
- Add `BusinessHoursComponent` with calendars and holiday rows.
- Add `NotificationRulesComponent` for email/SMS SLA alerts.

Acceptance:

- Admin can add/edit/delete/reorder assignment rules.
- Rules show validation errors for invalid JSON/empty targets.
- Escalation rules preview affected SLA policies.
- Business hours and holidays are editable and used by SLA calculations.
- Email/SMS alerts can be enabled without exposing secrets.

### Slice C - Communication Channels

Closes: email inbound UI, WhatsApp session UI, SMS UI, chat session static mockup, chat AI sidebar.

Backend:

- Normalize inbound channel conversations into one `ChannelConversation` read model, or extend existing ticket messages with channel metadata.
- Ensure webhooks for email/WhatsApp/SMS create searchable conversations/ticket messages.
- Add `GET /api/channels/conversations` and `GET /api/channels/conversations/{id}`.
- Push `ChannelMessageReceived` through `MainHub`.

API shape:

```csharp
public sealed record ChannelConversationDto(
    Guid Id,
    string Channel,
    string CustomerDisplayName,
    Guid? CustomerId,
    Guid? TicketId,
    string Status,
    DateTimeOffset LastMessageAt,
    string LastMessagePreview);
```

Frontend:

- Add channel inbox tabs: All, Email, WhatsApp, SMS, Live chat, Web form.
- Convert chat session page to bind to store/API only.
- Add reply composer with channel-specific send constraints.
- Add ticket-link/create flow from channel conversation.
- Add AI sidebar that calls ticket AI when a ticket is linked, otherwise summarizes transcript locally and offers handoff/create-ticket.

Acceptance:

- No static channel cards remain.
- Agent can filter conversations by channel and status.
- Replies call the channel API and append optimistically only after success.
- Realtime events update current conversation.
- AI sidebar degrades honestly when no linked ticket exists.

### Slice D - Agent Workspace

Closes: `/agent-workspace`, tasks/reminders, quick replies, team collaboration, internal chat, presence.

Backend:

- Expose `TicketTask` CRUD and due/reminder filters.
- Expose `QuickReply` CRUD, search, category, and active flag.
- Add `InternalThread`/`InternalMessage` if ticket notes are insufficient for agent-to-agent chat.
- Add presence events in `MainHub`.

API shape:

```csharp
public sealed record CreateTicketTaskRequest(
    Guid TicketId,
    string Title,
    DateTimeOffset? DueAt,
    Guid? AssigneeId,
    string? ReminderChannel);
```

```ts
export interface QuickReplyDto {
  readonly id: string;
  readonly title: string;
  readonly body: string;
  readonly channel: 'Email' | 'WhatsApp' | 'Sms' | 'LiveChat' | 'Any';
  readonly isActive: boolean;
}
```

Frontend:

- Add `/agent-workspace` route and nav item.
- Workspace has assigned tickets, tasks due today, quick replies, internal thread, and presence rail.
- Composer supports inserting quick replies into ticket/chat message surfaces.

Acceptance:

- Agent can create/complete/reassign tasks.
- Reminder state is visible and persisted.
- Quick replies can be searched and inserted.
- Internal messages are private to staff and never visible in portal timeline.
- Presence comes from SignalR state, not hardcoded avatars.

### Slice E - Knowledge Base

Closes: version history, category picker, insights static card.

Backend:

- Reuse `ContentVersion` and category tree.
- Add KB analytics endpoint if current list page is insufficient for global insight totals.

Frontend:

- Create/edit form includes category picker.
- Edit view renders version history.
- Insights compute from API, or call analytics endpoint for full counts.

Acceptance:

- Category assignment persists.
- Version history is visible with timestamp, author, and change summary.
- Insight totals match API data and never use hardcoded literals.

### Slice F - AI Features

Closes: suggested reply button, chat-session AI sidebar static, KB article dead route.

Backend:

- Existing ticket AI endpoints remain source of truth.
- Add optional `POST /api/ai/chats/{sessionId}/suggest-reply` for chat sessions not linked to tickets.
- Ensure KB citations return route-safe article ids.

Frontend:

- AI panel exposes draft reply, summary, categories, solutions.
- KB links route to existing admin or portal KB surfaces.
- Chat sidebar uses ticket AI when linked, chat AI when unlinked.

Acceptance:

- Suggested reply button calls `AiApi.draftReply`.
- Insert puts selected draft into the current composer only.
- KB article link never 404s.
- Not-configured AI returns an unavailable state, not hidden controls.

### Slice G - Portal Account Recovery

Closes: forgot password link.

Backend:

- Add password reset request and completion endpoints with one-time token.
- Token is hashed at rest, expires, and is rate limited.

API shape:

```csharp
public sealed record RequestPasswordResetRequest(string Email);
public sealed record CompletePasswordResetRequest(string Token, string NewPassword);
```

Frontend:

- Add portal and admin forgot-password routes.
- Add request form, success state, reset form, invalid/expired token state.

Acceptance:

- Login screens link to reset request.
- Response does not reveal whether email exists.
- Expired/used token is refused.

### Slice H - Reports And Management

Closes: PDF/CSV export, trend percentages, dashboard CSAT, per-agent drill-down.

Backend:

- Add comparison-period metrics to dashboard/report DTOs.
- Add export endpoint for CSV and PDF.
- Add agent drill-down endpoint.

API shape:

```csharp
public sealed record KpiMetricDto(
    string Key,
    decimal Value,
    decimal? PreviousValue,
    decimal? TrendPercent,
    string Direction);
```

```csharp
[HttpGet("ticket-volume/export")]
public Task<FileResult> ExportTicketVolume([FromQuery] ReportFilter filter, [FromQuery] string format);
```

Frontend:

- Replace hardcoded trends with DTO values.
- Add report export buttons.
- Add agent detail drawer/route.

Acceptance:

- Dashboard trends are derived from backend comparison data.
- CSAT comes from survey report endpoint.
- CSV/PDF exports download files with active filters.
- Agent row drill-down shows tickets, SLA, CSAT, and resolution time.

### Slice I - Security And Administration

Closes: user edit, department assignment, audit export, profile timezone/job title/notifications/billing tabs.

Backend:

- Extend user profile with `DepartmentId`, `JobTitle`, `TimeZone`, notification preferences, billing metadata.
- Add admin update user endpoint.
- Add audit export endpoint or frontend export for current page.

Frontend:

- Users row menu supports edit, activate/deactivate, assign department, reset password invite.
- Profile page has real Account, Notifications, Billing tabs.

Acceptance:

- User department column binds real data.
- Admin edit persists profile and roles with authorization.
- Audit export downloads active filter results.
- Profile preference tabs load and save real data.

### Slice J - Integrations

Closes: hardcoded Gmail/WhatsApp/SMS cards, ERP connector, external API configs, hardcoded API key, decorative configure buttons.

Backend:

- Reuse `ExternalApiConfigurationsController`.
- Add provider capabilities, health checks, masked credentials, rotate/revoke key endpoints.
- Add ERP connector configuration type.

Frontend:

- Integrations page reads configs from API.
- Add/configure/resume buttons open provider-specific forms.
- API key field is masked and copied only by explicit action.

Acceptance:

- No hardcoded `sk_live_...` string remains.
- Cards render API config status and last health check.
- Configure saves provider config.
- Resume opens incomplete configuration step.
- Secrets are never shown after save.

### Slice K - Platform Structure And Branding

Closes: multi-branch UI, multi-team UI, branding form miswire, runtime branding, logo upload, global default language, department tree.

Backend:

- Reuse Branches/Teams/Departments controllers.
- Add default language platform setting.
- Add upload asset endpoint for logo if data URLs are not acceptable.
- Add hierarchy query for branch -> department -> team.

Frontend:

- Add Branches and Teams screens or tabs under Platform Settings.
- Add organization tree view.
- Fix branding field bindings and runtime CSS application.
- Add default language selector and save to platform setting.

Acceptance:

- Admin can create/edit/deactivate branches and teams.
- Department tree is visible under Platform Settings.
- Branding changes apply in admin and portal after save/reload.
- Logo upload stores a durable URL.
- Default language is global and browser override still works.

## 6. Security And Data Rules

- Customer MRR is visible only to Admin/Supervisor unless product approves broader visibility.
- API keys are write-only after save; list endpoints return masked values.
- Channel webhooks validate provider signatures.
- Internal chat is staff-only and excluded from customer portal responses.
- Export endpoints enforce report-scoping rules and audit the export.
- Password reset tokens are hashed, single-use, and expire.

## 7. Test Strategy

Backend:

- Domain tests for new entity invariants.
- Handler tests for validation and authorization-sensitive branches.
- Controller/integration tests for route, envelope, status mapping, and auth.
- Migration tests for schema changes and indexes.

Frontend:

- API client tests for every new route and payload.
- Component tests for loading/empty/error/loaded states.
- Interaction tests for every button/menu/dialog.
- Translation and RTL checks for every new template.
- Playwright Stitch journey: customer, agent, supervisor, admin, portal.

Evidence gate:

```text
dotnet test backend/CustomerSupport.slnx
cd frontend
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test
```

## 8. Done Definition

A gap is done only when:

- Spec, plan, and task file are updated.
- Backend code, frontend code, tests, translations, and migration are complete where required.
- Decorative controls are removed or wired.
- Acceptance tests fail before implementation and pass after implementation.
- Build/test output is recorded in the task evidence section.
- Screens are compared against Stitch reference at desktop and mobile widths.
