# Communication Channels Frontend

**Epic:** `EPIC-03 Communication Channels`  
**Features:** `FEAT-24` WhatsApp, `FEAT-25` SMS conversations, `FEAT-26` Live chat, `FEAT-27` Web forms  
**Related backend spec:** `EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md`

## Problem

The backend channel work has no complete browser surface. Staff cannot distinguish channel messages
in the ticket timeline, claim waiting live-chat sessions, or exchange live-chat messages without a
reload. Anonymous visitors have no live-chat or public web-form entry point. Existing customer ticket
submission and the existing AI chat are different features and must not be silently reused.

## Assumptions

- **A1.** Staff screens live in `admin-app`; anonymous customer screens live in `portal-app`.
  Anonymous routes remain outside the authenticated `/app` route group in
  `frontend/projects/portal-app/src/app/app.routes.ts:13-32`.
- **A2.** `TicketMessage.Channel` uses the backend values `System`, `Email`, `WhatsApp`, `SMS`,
  `WebForm`, and `LiveChat`. The frontend union must match these values exactly.
- **A3.** The existing ticket message form is also the channel reply entry point. Its current channel
  source is `frontend/projects/common/src/lib/tickets/ticket.api.ts:76-99` and its UI is
  `admin-app/src/app/features/tickets/ticket-messages.component.html:25-39`.
- **A4.** Agents use the authenticated `/hubs/main` connection through `RealtimeService`. Anonymous
  visitors use the backend's narrow `/hubs/chat` connection with an opaque session token.
  `RealtimeService` cannot be used unchanged for anonymous users because it starts only when
  `SessionStore.isAuthenticated()` is true (`common/src/lib/realtime/realtime.service.ts:48-57`).
- **A5.** The backend returns the existing `Response<T>` envelope. Angular services consume unwrapped
  data through the existing envelope interceptor, as documented in `common/src/lib/notifications/notification.api.ts:6-9`.
- **A6.** All new visible strings have English and Arabic translations. No template literals or
  hardcoded user-facing text are added; the existing no-hardcoded-strings test remains authoritative.
- **A7.** Wait time is calculated from the server `createdAt` timestamp using the browser clock. The
  server remains authoritative for session state and authorization.
- **A8.** The existing `portal-app/features/chat/chat.component.ts` is the FEAT-21 AI assistant. It
  is not a live-chat component and must not be modified for this feature.
- **A9.** `US-607` is the separate ticket live queue/reporting feature. Its queue API or hub must not
  be reused for live-chat sessions.

## Out of Scope

- Typing indicators and read receipts.
- CAPTCHA integration.
- Skills-based routing or automatic agent assignment.
- Automatic conversion of abandoned sessions into tickets.
- WhatsApp provider configuration screens or live Meta credentials.
- Changes to the FEAT-21 AI assistant.
- Changes to the authenticated portal ticket-submission flow.

## Acceptance Criteria

- **FB-1 / CC-24:** Given a ticket message with any supported channel, when the ticket timeline
  renders it, then it uses one shared channel-label mapping with the existing direction icon, sender,
  timestamp, subject, and body treatment. The template has no System/Email-only conditional.
- **FB-2 / CC-25:** Given waiting live-chat sessions, when an authenticated agent opens the queue,
  then sessions are listed with customer information, wait time, status, and a claim action. A
  successful claim navigates to that session's transcript.
- **FB-3 / CC-26:** Given an active agent chat session, when a chat message is pushed through SignalR,
  then it appears in the transcript without polling or a page reload. Messages for another session
  are ignored.
- **FB-4 / CC-14:** Given an anonymous visitor starts live chat, when the start request succeeds,
  then the client stores only the opaque session token and connects to the session-scoped chat hub.
- **FB-5 / CC-16:** Given an active anonymous chat session, when either party sends a message, then
  the other party sees it through SignalR and the client displays waiting, active, closed, abandoned,
  connecting, and reconnecting states.
- **FB-6 / CC-20/CC-21:** Given a valid public web-form submission, when it succeeds, then the
  visitor sees only the returned ticket reference. Invalid fields show field-keyed errors without
  creating a second client-side validation contract.
- **FB-7 / CC-22:** Given a filled honeypot or throttled submission, when the form is submitted, then
  the UI presents the same success state as a normal submission and does not expose the defense.
- **FB-8 / CC-28/CC-29:** Given either anonymous surface, when a request is made, then the browser
  never submits a customer id, ticket id, provider secret, or full webhook payload.
- **FB-9:** Given either locale, when any new channel or chat screen renders, then all visible strings
  have English/Arabic translations and layout uses the existing logical RTL-friendly utilities.

## Design

### Shared library

Add channel contracts under `frontend/projects/common/src/lib/channels/`:

- `chat.model.ts` for session/message DTOs matching the backend contract.
- `chat.api.ts` for typed waiting, start, claim, transcript, and send calls.
- `chat.store.ts` for signal-based active-session state and pushed messages.
- `channel-labels.ts` for the single channel-to-translation-key mapping used by the timeline.
- `web-form.api.ts` for the anonymous form request.

Export public contracts from `common/src/public-api.ts`, following the existing ticket and realtime
exports at lines 28-30 and 77-79.

### Admin application

- Extend `MESSAGE_CHANNELS` and `TicketMessage` in `common/src/lib/tickets/ticket.api.ts`.
- Generalize the existing timeline in `ticket-messages.component`; do not create a second timeline.
- Add `features/chat/chat-queue.component` and `features/chat/chat-session.component`.
- Add `/chat` and `/chat/sessions/:id` under the authenticated admin shell.
- Add a `Live chat` item to `NAV_ITEMS` in `admin-app/src/app/layout/shell.component.ts:53-65`.
- Reuse `AsyncState` loading, empty, and error states from `ticket-messages.component.ts:57-95`.
- Reuse `RealtimeService.on()` (`realtime.service.ts:107-110`) for authenticated chat events.

### Portal application

- Add a separate `features/live-chat/live-chat-widget` component.
- Add a separate `features/web-form/web-form` component.
- Register both as anonymous routes beside the existing public routes, not inside `/app`.
- Use a small anonymous SignalR client for `/hubs/chat?token=...`; do not loosen `/hubs/main`.
- Keep the existing authenticated `features/tickets/submit.component` and AI `features/chat/chat.component`
  unchanged.

### Error and security behavior

HTTP services return typed observables and allow the envelope interceptor to produce `ApiError`.
Components map field errors using the existing `fieldError()` pattern in
`ticket-messages.component.ts:148-157`. Anonymous components hold only the session token and never
choose another customer, ticket, or session id.
