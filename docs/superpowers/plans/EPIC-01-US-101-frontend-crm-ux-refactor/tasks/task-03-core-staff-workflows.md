# Task 03: Core Staff Workflows

**Status:** Partially implemented  
**Criteria:** `AC-507`, `AC-508`, `AC-509`, `AC-510`, `AC-511`  
**Scope:** Agent dashboard, ticket queue/create/detail, customer list/create/detail, chat context.

## Files To Read First

- `frontend/projects/admin-app/src/app/features/dashboard/dashboard.component.ts`
- `frontend/projects/admin-app/src/app/features/dashboard/dashboard.component.html`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.html`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.html`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ai-panel.component.ts`
- `frontend/projects/admin-app/src/app/features/customers/customer-list.component.ts`
- `frontend/projects/admin-app/src/app/features/customers/customer-detail.component.ts`
- `frontend/projects/admin-app/src/app/features/chat/chat-queue.component.ts`
- `frontend/projects/admin-app/src/app/features/chat/chat-session.component.ts`

## Intent

Make daily support work easier: agents should know what to do next, supervisors should see queue
risk, and customer/ticket detail should support fast triage, reply, assignment, resolution, notes,
attachments, and history review.

Use `stitch_smart_support_ticketing_crm` as the composition reference:

- `ai_powered_agent_workspace` drives dashboard, queue, active work, quick action, and AI suggestion
  placement.
- `ticket_detail_chatbot` drives ticket detail header, metadata strip, conversation/history split,
  action grouping, and AI assist side rail.
- `customer_360_history` drives customer detail, attachments, notes, open tickets, and interaction
  history layout.

## Required Changes

- Dashboard: emphasize assigned work, SLA risk, unassigned tickets, recent activity, and quick
  actions. Show unavailable reminders/tasks/collaboration honestly if no data source exists.
- Ticket queue: make customer, subject, category, priority, status, assignee, channel, SLA risk,
  escalation, and updated time scan-friendly in desktop rows and mobile stacked items.
- Ticket create/edit forms: improve field grouping, validation visibility, attachment placement,
  priority/category controls, and action footer.
- Ticket detail: create a strong identity header, conversation timeline, history, metadata rail, SLA
  banner, assignment/status actions, quick reply area, attachments, and AI panel hierarchy.
- Customer detail: separate contact profile, open tickets, interaction history, notes, attachments,
  and actions.
- Chat/channel screens: make source channel, customer identity, session status, handoff, and linked
  ticket states visible.
- Error/reload: every dashboard, ticket, customer, and chat panel must show loading, empty, error,
  retry, unavailable, and action-busy states without requiring a page refresh.
- AI usage: summaries, suggested replies, categories, and KB solutions must be grouped as
  reviewable suggestions and must not replace the original ticket/customer data.

## Implementation Notes

- Use existing APIs and DTOs from `common/src/lib/tickets`, `customers`, `channels`, and `ai`.
- If a feature area lacks API data, keep its designed region but label it unavailable or disabled.
- Avoid turning dense work screens into marketing-style card galleries.
- Keep tables keyboard navigable and mobile layouts readable without horizontal page scroll.

## Code Context And Examples

Ticket queue desktop row target:

```html
<tr class="border-b border-border-subtle hover:bg-surface-bright">
  <td class="px-4 py-3">
    <a [routerLink]="['/tickets', ticket.id]" class="font-medium text-primary">
      {{ ticket.reference }}
    </a>
    <p class="text-body-sm text-on-surface-variant">{{ ticket.subject }}</p>
  </td>
  <td class="px-4 py-3">
    <span class="text-body-sm text-on-surface">{{ ticket.customerName }}</span>
    <p class="text-body-sm text-on-surface-variant">{{ ticket.categoryName }}</p>
  </td>
  <td class="px-4 py-3">
    <cs-badge kind="priority" [value]="ticket.priority" />
  </td>
  <td class="px-4 py-3">
    <cs-badge kind="status" [value]="ticket.status" />
  </td>
  <td class="px-4 py-3">
    <cs-channel-pill [channel]="ticket.channel ?? 'web'" />
  </td>
  <td class="px-4 py-3">
    <cs-sla-pill [state]="ticket.slaState ?? 'healthy'" [dueAt]="ticket.responseDueAt" />
  </td>
  <td class="px-4 py-3 text-end">
    <cs-button variant="secondary" [routerLink]="['/tickets', ticket.id]">
      {{ 'common.open' | t }}
    </cs-button>
  </td>
</tr>
```

Ticket detail layout target:

```html
<section class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_22rem]">
  <div class="min-w-0 space-y-4">
    <header class="rounded-lg border border-border-subtle bg-surface-lowest p-4">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p class="font-mono text-data-mono text-on-surface-variant">{{ ticket().reference }}</p>
          <h1 class="font-display text-headline-lg text-on-surface">{{ ticket().subject }}</h1>
        </div>
        <div class="flex flex-wrap gap-2">
          <cs-badge kind="priority" [value]="ticket().priority" />
          <cs-badge kind="status" [value]="ticket().status" />
        </div>
      </div>
    </header>

    <app-ticket-messages [ticketId]="ticket().id" />
  </div>

  <aside class="space-y-4">
    <app-ai-panel [ticket]="ticket()" />
    <app-ticket-metadata [ticket]="ticket()" />
  </aside>
</section>
```

Customer detail composition target:

```html
<section class="grid gap-4 xl:grid-cols-[20rem_minmax(0,1fr)_18rem]">
  <aside aria-label="Customer contact">
    <!-- contact details, branch, department, customer since -->
  </aside>
  <main class="min-w-0">
    <!-- open tickets, interaction history, timeline -->
  </main>
  <aside aria-label="Customer notes and files">
    <!-- notes, attachments, guarded actions -->
  </aside>
</section>
```

Example unavailable state for missing workflow data:

```html
<cs-card [heading]="'dashboard.reminders.title' | t">
  <cs-empty-state
    [title]="'dashboard.reminders.unavailableTitle' | t"
    [message]="'dashboard.reminders.unavailableMessage' | t"
  />
</cs-card>
```

## Suggested Tests

- `AC507_DashboardPrioritizesAssignedWorkAndSlaRisk`
- `AC508_TicketQueueRowsExposeCrmCriticalState`
- `AC509_TicketDetailSupportsTriageReplyAssignmentAndAi`
- `AC510_CustomerDetailSeparatesProfileHistoryNotesAndAttachments`
- `AC511_FormValidationAndPrimaryActionsRemainVisible`

## Verification

Run from `frontend/`:

```text
npx ng test admin-app --watch=false --include='**/dashboard.component.spec.ts'
npx ng test admin-app --watch=false --include='**/ticket-*.component.spec.ts'
npx ng test admin-app --watch=false --include='**/customer-*.component.spec.ts'
npx ng build admin-app
```

## Execution Record

| Item | Result |
|---|---|
| Tests added | Updated dashboard lifecycle expectations, ticket queue column expectation, ticket detail transition expectation, and shared pagination coverage to match the current eight-status CRM workflow. |
| Commands run | `npx ng test admin-app --watch=false` passed: 28 files, 187 tests. `npx ng build admin-app` passed with the existing initial bundle budget warning: 590.24 kB versus 500 kB. |
| Deviations | Ticket/customer lists now use `cs-pagination`; ticket/customer create forms now use `cs-action-bar` with visible cancel and submit affordances. Ticket queue exposes channel and SLA state with fallbacks. Full ticket detail/customer detail layout refactor remains for the next pass. |
| Commit | Pending |
