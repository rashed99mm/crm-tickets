# EPIC-12-US-000: As-Built Implementation Alignment

This plan is the final implementation reference for behavior delivered across the CRM hosts. It
uses real code touchpoints rather than date-based task history.

## Architecture

The solution runs two .NET 10 web API hosts over the shared application/domain/infrastructure
composition:

```text
CustomerSupport.InternalApi  -> staff CRM, tickets, reports, chat, notifications
CustomerSupport.ExternalApi  -> portal tickets, public KB, anonymous live chat
```

Both hosts use the same response envelope and database configuration. Host-specific controllers
enforce the boundary; the portal must never call the internal staff chat endpoints.

## Authorization

Support chat is restricted consistently at the API and route layers:

```csharp
// Api.Shared/Extensions/AuthorizationExtensions.cs
.AddPolicy("ChatSupport", policy =>
    policy.RequireRole("Agent", "Supervisor", "Admin"));

// InternalApi/Controllers/ChatController.cs
[Authorize(Policy = "ChatSupport")]
public class ChatController(IMediator mediator) : ControllerBase
```

The admin routes mirror this policy with `roleGuard('Agent', 'Supervisor', 'Admin')`. Customer
portal live chat uses `/api/external/chat/*` and an opaque session token instead.

## Live Chat State

The domain permits claiming a Waiting session and sending agent messages only while Active. The
queue therefore does not offer Claim for an Active session:

```html
@if (session.status === 'Waiting') {
  <cs-button (click)="claim(session)">{{ 'chat.queue.claim' | t }}</cs-button>
} @else if (session.status === 'Active') {
  <cs-button variant="secondary" (click)="openSession(session)">
    {{ 'chat.queue.open' | t }}
  </cs-button>
}
```

The component also guards the API call so a stale row cannot submit an invalid transition:

```ts
claim(session: ChatSessionDto): void {
  if (session.status !== 'Waiting' || this.claimingId()) return;
  // claim and navigate to /chat/sessions/:id
}
```

## Portal Ticket Detail

Ticket detail keeps history, attachments, reply, and survey behavior, and adds a link to the
existing external live-chat flow:

```html
<a routerLink="/live-chat">
  <cs-icon name="chat" />
  {{ 'portal.liveChat.start' | t }}
</a>
```

This deliberately starts a public session instead of sending a customer token to the internal
`/api/chat` controller.

## Notifications

Ticket creation and status changes publish in-app notifications to the customer, assignee, actor,
and support roles. Recipient keys are deduplicated per event and recipient, so a status change does
not create duplicate notifications when the actor is also the assignee or an administrator.

## Verification

Required checks for future changes:

```text
dotnet build backend/src/CustomerSupport.InternalApi/CustomerSupport.InternalApi.csproj --no-restore
npx ng build admin-app --configuration development
npx ng build portal-app --configuration development
npx ng test admin-app --watch=false --include=projects/admin-app/src/app/features/chat/chat-queue.component.spec.ts
```

The focused chat queue spec covers Waiting claim, empty queue, and Active-session protection.

