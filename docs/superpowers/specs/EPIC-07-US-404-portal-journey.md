# Customer portal journey (FEAT-22, US-401–US-415 — backend + frontend remainder)

**Date:** 2026-08-27
**Feature:** `FEAT-22` Customer portal
**Stories:** US-401..US-415, excluding the shipped `home`+`signup` slice (EPIC-07-US-404-portal-home-and-signup-design).
**Status note:** Written before implementation, per the SDD gate. Criteria are scoped `PJ-n` and each names the
story `AC`/trace it satisfies, so tests can be named after them without colliding with the global `AC-n` numbers.

## Problem

The portal shell signs customers in and the public home/signup slice is shipped, but a signed-in customer has
nowhere real to go: the portal's ticket screens are scaffolds that post through the **staff** `TicketApi` — a
`customer_id` picker, staff `/api/Tickets` endpoints, no customer-scoped auth — so they fail against the external
host, and the backend has no customer record linked to a signup, no `customerId` claim, and no customer-facing
ticket or survey surface at all. Everything the brief's EPIC-07 requires after authentication is still missing:

- registration creates an identity user but **no `Customers` row** and no link back (US-401);
- tokens carry **no `customerId` claim**, so nothing can be scoped to the signing-in customer (US-402/403);
- there are **no portal endpoints** — submit, my-tickets, detail, reply, survey (US-404..409);
- the portal's Angular ticket screens call staff endpoints, and the reply/survey screens do not exist (US-410..415).

## Assumptions

A1. **Register stays a two-call flow, and the JWT appears at login, not register.** US-401 TC-01 worded as
   "201 with JWT" is superseded by the shipped portal home/signup agreement (A2 there): `POST /api/Auth/register`
   returns 201 + user id, `POST /api/Auth/login` returns the JWT. The "JWT with a `customerId` claim" requirement
   (US-401 AC-1 / US-402 AC-1) is therefore satisfied by the login token — which now carries the claim. The
   register endpoint itself does not start issuing tokens. Recorded because the two story files differ from the
   shipped flow; the shipped flow wins.

A2. **`CustomerId` on `ApplicationUser` is nullable.** US-401/402's `AspNetUsers` DDL shows `CustomerId NOT NULL`,
   but the same host serves staff registration (InternalApi), where no `Customers` row exists. Nullable
   `Guid? CustomerId` — set by portal registration, `null` for staff — is the correct shape; the DDL is
   aspirational.

A3. **Portal registration creates the `Customers` row only when the request comes through the portal host.**
   Both hosts share `RegisterCommand`. A new `IsPortalRegistration` flag is set by `ExternalApi` and unset by
   `InternalApi`, so a staff user registering on the internal host never gets a spurious `Customers` row.

A4. **Atomicity of the customer + identity write.** The `RegisterCommandHandler` calls `customers.AddAsync` on
   the same scoped `AppDbContext` that `IIdentityUserService.CreateAsync` (UserManager over `AppDbContext`)
   saves, so adding the customer before `CreateAsync` lands both rows in the identity store's single
   `SaveChanges` — one transaction. If the customer's unique email index rejects the duplicate, the whole save
   rolls back and the handler returns a conflict; no orphan user or customer survives.

A5. **`SenderType=Customer` maps onto the shipped `TicketMessage` model.** US-406/407's `Messages` DDL
   (`SenderId`, `SenderType`, `Content`) does not match the implemented `TicketMessage` (AC-101: `Direction`,
   `Channel`, `SenderId`, `Body`). A portal reply is recorded as `TicketMessage` with `Direction="Inbound"`,
   `Channel="Portal"`, `SenderId` = the signed-in user's id (never accepted in the body) — the existing schema's
   faithful encoding of "written by the customer". `"Portal"` is added to `AllowedChannels`.

A6. **Ticket `Source` is set via the existing `SetSource`. Domain source of truth US-404's "Channel=Portal"
   maps to `Ticket.Source = "Portal"`** (the implemented column BR-21 stamps — see `SetSource`, used by inbound
   channel ingestion) — set by the portal create flow, left null for staff-created tickets as today. The
   `CreateTicketCommand` gains an optional `Source`; staff callers pass nothing and behaviour is unchanged.

A7. **Portal submission keeps `description` required at the domain level.** US-411's note "description is
   optional" collides with `Ticket.Create`'s required-description invariant (AC-30). The portal form keeps
   subject, category, description and priority (default `Normal`), matching the `submit_ticket` mockup; the
   story's note is treated as superseded. `categoryId` stays required (AC-29 style ownership of the value is the
   portal's: the picker lists seeded active categories).

A8. **A survey is accepted for `Resolved` or `Closed` tickets** (BR-24 "one survey per resolved ticket" — US-409
   TC-02 says "resolved", the CC/SLA value set is `Resolved`/`Closed`, and the customer can no longer reply to a
   closed ticket, so closed is the more restrictive parent that still admits one). Re-submission → 409.

A9. **Portal list is unpaged.** US-405 "Pagination may be added later." Returns the customer's tickets by
   `CreatedAt` descending with status + reference, which is what the `my-tickets` mockup renders.

A10. **`surveySubmitted` rides in the detail DTO** so the UI can satisfy US-415 AC3 ("already submitted" hides
   the form) with the same request that renders the ticket.

## Out of scope

- OTP, email verification, password reset, or any phone-based flow for customers (own track).
- Staff-facing change anywhere in the internal host's behaviour — `Source` is optional and unset by staff call
  sites; `RegisterCommand.IsPortalRegistration` defaults to false.
- The knowledge base and live-chat/contact screens — already route to shared/KB endpoints, out of this journey.
- Customer "profile" screen or editing the captured phone after signup (US-401's own follow-ups are separate).
- Admin approval/deactivation of customer accounts.

## Acceptance criteria

Legend: each `PJ-n` names the story (and its `AC`) it forces green.

**Backend — auth foundation**

- PJ-1 (US-401 AC-1): Given the portal host receives a valid registration, then a `Customers` row exists
  (email, display name from first+last, phone from the request), an `ApplicationUser` exists with
  `CustomerId` set to that row, and the response is 201 with the new user id. (Story TCs 02, 03)
- PJ-2 (US-401 AC-1): Given an email already registered in `Customers`, when the portal registers it, then the
  registration fails with a conflict rather than creating an orphan identity user. (TC-04 duplicate w/ identity
  side; the customer-unique-index side is A4.)
- PJ-3 (US-402 AC-1): Given a customer registers then logs in, when login succeeds, then the returned JWT
  contains a `customerId` claim equal to the customer record's id. (TC-01)
- PJ-4 (US-402 AC-1): Given staff (no customer link) logs in, then the token carries no `customerId` claim and
  login behaves exactly as before. (regression — no story TC)

**Backend — ticket + reply + survey surface**

- PJ-5 (US-404 AC-4): Given an authenticated customer, when `POST /api/portal/tickets` is submitted with
  subject/description/category/priority, then 201 with the ticket id; `Ticket.Source == "Portal"` and
  `Ticket.CustomerId ==` the JWT's customer (never the body's). Unauthenticated → 401. (TC-01, 02, 04)
- PJ-6 (US-404 AC-4): Given a ticket created, then `ReferenceNumber` is non-null and matches the existing
  `TKT-nnnnnn` generator. (TC-03)
- PJ-7 (US-405 AC-5): Given a customer, `GET /api/portal/tickets` returns only their tickets with
  referenceNumber, status and createdAt (unpaged, newest first), and `[]` when there are none. (TC-01..04)
- PJ-8 (US-406 AC-6): Given a customer owns a ticket, `GET /api/portal/tickets/{id}` returns subject, reference,
  status, description, and the message history (oldest first) plus `surveySubmitted`. (TC-01, 02)
- PJ-9 (US-403 AC-3 / US-406 TC-03, TC-04): Given customer A's token requests customer B's ticket (list → never
  returned; detail/reply/survey → 403 "not your ticket"), and an unknown ticket id → 404. (TC-02, 03, 04)
- PJ-10 (US-407 AC-7): Given a customer owns the ticket, `POST /api/portal/tickets/{id}/reply` with non-empty
  content → 201 and a `TicketMessage` recorded with `Direction=Inbound`, `Channel=Portal`, sender = the session
  (no id in body). Empty content → 400. (TC-01..04)
- PJ-11 (US-408 AC-8): Given `SurveyResponse.Create(ticketId, rating, freeText)`, then it stores TicketId,
  Rating (1–5), FreeText (≤2000, optional) and CreatedAt; rating <1 or >5 throws; the `TicketId` unique index
  allows exactly one row per ticket. (TC-01..05)
- PJ-12 (US-409 AC-9): Given a customer owns a `Resolved`/`Closed` ticket with no survey, then
  `POST /api/portal/tickets/{id}/survey` with rating 1–5 → 201 persisted. Open ticket → 400; duplicate → 409;
  other customer → 403; rating outside 1–5 → 400. (TC-01..05)

**Frontend — portal screens + client**

- PJ-13 (US-411 AC-11): Given the portal submit screen, then it renders subject, category, priority and
  description; empty subject → client validation, no HTTP call; valid submit → `POST /api/portal/tickets`
  (no customerId in the body) and a confirmation, navigated to my-tickets. (TC-01..03 partially — TC-04 success
  message or navigation, matching the shipped flow's navigation)
- PJ-14 (US-412 AC-12): Given my-tickets loads, then `GET /api/portal/tickets` fires and each row shows status
  badge + referenceNumber; empty → empty-state message; row click navigates to `/app/tickets/{id}`.
  (TC-01..04)
- PJ-15 (US-413/414 AC-13/14): Given the detail screen loads by route id, then `GET /api/portal/tickets/{id}`
  fires and status, reference, description and message history render; the reply form shows only when status is
  not `Closed`; a non-empty reply → `POST .../reply`, clears the field and refreshes history; empty reply →
  client validation, no call. (TC-01..05 + US-414 TC-01..04)
- PJ-16 (US-415 AC-15): Given a resolved/closed ticket detail, then the survey form (1–5 rating, optional
  free text) renders; no rating → client validation; submit → `POST .../survey`; when the detail returns
  `surveySubmitted: true` the form is hidden and a thank-you shows. (TC-01..05)

## Design

### Backend

**Domain**

- `ApplicationUser`: add `public Guid? CustomerId { get; private set; }` and a setter used only by portal
  registration (`LinkCustomer(Guid customerId)` — throws on empty; no general setter).
- `TicketMessage.Create`: add `"Portal"` to `AllowedChannels` (A5).
- `SurveyResponse` (new, in `Entities/Survey/SurveyResponse.cs`): `BaseEntity, IAppendOnlyEntity`; static
  `Create(Guid ticketId, int rating, string? freeText)`; validates ticketId non-empty, rating 1–5 (throws),
  freeText ≤ 2000 (throws). Mapping in `Persistence/Configurations/SurveyResponseConfiguration.cs`: unique
  index on `TicketId`, `Rating` int, `FreeText` nvarchar(2000) null, `CreatedAt` required.

**Application**

- `RegisterCommand`: add `bool IsPortalRegistration = false`.
- `RegisterCommandHandler`: when the flag is set — `var customer = Customer.Create($"{request.FirstName}
  {request.LastName}".Trim(), request.Email, request.PhoneNumber)`; `await customers.AddAsync(customer)`;
  `user.LinkCustomer(customer.Id)`; then the existing `CreateAsync`. Wrap the save's unique-violation in a
  catch → `Customer.EMAIL_EXISTS` conflict (A4). Inject `IRepository<Customer>`, `IUnitOfWork`,
  `IDbExceptionTranslator`.
- New `AuthClaimTypes` const (`Features/Auth/AuthClaimTypes.cs`): `public const string CustomerId = "customerId";`
- `LoginCommandHandler` + `RefreshTokenCommandHandler`: build
  `user.CustomerId is { } cid ? [new Claim(AuthClaimTypes.CustomerId, cid.ToString())] : null` and pass it to
  `GenerateAccessToken` (staff stays `null` — PJ-4).
- `CreateTicketCommand`: add `string? Source`. `CreateTicketCommandHandler`: after `Ticket.Create`, when
  `request.Source` is set, `ticket.SetSource(request.Source)`. `CreateTicketCommandValidator`: `Source`, when
  present, must be one of `{ Portal, WebForm, WhatsApp, SMS, Email, LiveChat }` (`Source.INVALID`).
- `PortalTicketsQuery` (Features/Portal), `GetPortalTicketsQueryHandler`: own tickets via
  `IRepository<Ticket>` filtered `CustomerId == claim`, projecting
  `PortalTicketListItemDto(Id, Reference, Subject, Status, CreatedAt)` newest first.
- `GetPortalTicketDetailQuery`/handler: single ticket by id; 404 when missing; 403 when not owned; includes
  `PortalMessageDto` list (`TicketMessage` by `TicketId`, ORDER BY `SentAt`) and `SurveySubmitted` (bool from
  `IRepository<SurveyResponse>.ExistsAsync`).
- `CreatePortalReplyCommand`/handler: ownership check → 403; `TicketMessage.Create(ticketId, "Inbound",
  "Portal", null, body, userContext.UserId)`; add + save; FluentValidation: body required. Sender from session.
- `SubmitSurveyCommand`/handler: ownership → 403; ticket status must be `Resolved`/`Closed` else 400
  (`Survey.TICKET_NOT_RESOLVED`); `SurveyResponse.ExistsAsync(ticketId)` → 409 (`Survey.ALREADY_SUBMITTED`);
  `SurveyResponse.Create`; add + save. FluentValidation: rating inclusive 1..5, freeText ≤ 2000.
- `ApplicationErrors.Portal`: `TICKET_FORBIDDEN`, `SURVEY_ALREADY_SUBMITTED`, `SURVEY_TICKET_NOT_RESOLVED`,
  `SOURCE_INVALID`. SystemCode entries + Resources.yaml (en/ar) + `SystemCodeMap` + `ResponseExtensions` mapping
  (409 for duplicate survey, 403 forbidden) — internal status mapping follows the existing conventions.

**ExternalApi host**

- `AuthController.Register`: pass `IsPortalRegistration: true`. (InternalApi unchanged → false.)
- New `PortalController` (`[Authorize]`, route `api/portal`):
  - `POST api/portal/tickets` → `CreateTicketCommand` built with `CustomerId` from the `customerId` claim,
    `Source="Portal"`, subject/description/categoryId/priority from body. 201.
  - `GET api/portal/tickets` → own list. 200.
  - `GET api/portal/tickets/{id}` → detail. 200.
  - `POST api/portal/tickets/{id}/reply` → reply. 201.
  - `POST api/portal/tickets/{id}/survey` → survey. 201.
  - A missing/absent `customerId` claim → 403 (windowed scoping, PJ-9).
- New `CategoriesController` (`GET api/Categories`, `[Authorize]`) → active categories via the existing
  `GetCategoriesQueryHandler`, so the portal submit form's picker has a real endpoint on this host (A7).

**Migration**

- Single migration `AddCustomerLinkAndSurvey`: `AspNetUsers.CustomerId` (guid, nullable, FK → `Customers.Id`),
  `SurveyResponses` table (unique `TicketId`), `TicketMessage` channel value is a CLR constant — no schema.

### Frontend

- **common — `portal.api.ts`** (`PortalApi`, `providedIn: 'root'` over `HttpClient`):
  `listTickets(): Observable<PortalTicketListItem[]>` → `GET /api/portal/tickets`;
  `getTicket(id): Observable<PortalTicketDetail>`; `submitTicket({subject, description, categoryId, priority})`
  → `POST /api/portal/tickets` (no customer id); `reply(ticketId, body)` → `POST /api/portal/tickets/{id}/reply`;
  `submitSurvey(ticketId, {rating, comment})` → `POST /api/portal/tickets/{id}/survey`;
  `listCategories()` → `GET /api/Categories`.
  Interfaces: `PortalTicketListItem { id, reference, subject, status, createdAt }`,
  `PortalTicketDetail { id, reference, subject, description, status, priority, createdAt, messages, surveySubmitted }`,
  `PortalMessage { direction, body, sentAt }`. (+ `.spec.ts`)
- **portal-app components (rewire off `TicketApi`):**
  - `submit.component`: drop the customer picker (`customers` state, `searchCustomers`, `customerId` control);
    category from `PortalApi.listCategories`; submit `PortalApi.submitTicket`; success → navigate to
    `/app/tickets`. (PJ-13)
  - `list.component`: replace `PagedResult` paging/search with the unpaged `PortalApi.listTickets` array;
    status pill + reference per row; empty state; row → `/app/tickets/{id}`. (PJ-14)
  - `detail.component`: load via `getTicket`; render description + `messages` timeline + status/reference;
    embed the reply form (`CsInputField`/textarea + dirty validation, non-empty) shown when status ≠ `Closed`;
    embed the survey card (rating buttons 1–5 + optional comment) shown when `surveySubmitted === false` and
    status is `Resolved`/`Closed`; after reply → clear + reload; after survey → thank-you state. (PJ-15, PJ-16)
  - i18n keys (`translations.ts` en/ar): `portal.tickets.submit.*`, `portal.tickets.list.*`,
    `portal.tickets.detail.*`, `portal.tickets.reply.*`, `portal.tickets.survey.*`. (Branding/locale styling is
    FEAT-23's scope; this adds only the keys those screens need.)
  - Update `.spec.ts` for the three screens to the new client + criteria names, and the dashboard if it feeds
    off `TicketApi.countOnly` for portal metrics (switch to `listTickets` where the mockup means the customer's
    own tickets; otherwise leave).

## Error behavior

Portal endpoints return the standard envelope: 401 unauthenticated (missing/expired JWT), 403 when a claim is
present but the record belongs to another customer (`Portal.TICKET_FORBIDDEN`), 404 unknown id, 400 validation,
409 duplicate survey. No new HTTP statuses. Field errors map onto controls exactly as the staff forms do
(`ApiError.fieldError`).

## Testing

| Level | Covers |
|---|---|
| Backend integration (WebApplicationFactory over the external host) | PJ-1,2,3,5,6,7,8,9,10,12 — the external host boots, registers a customer, asserts `Customers` row + `CustomerId` link, logs in, checks the `customerId` claim, and drives every portal endpoint incl. the 403/404/409/400 windows; the register-login cycle is the only fixture the endpoints need |
| Backend integration (internal host) | PJ-3/4 regression — staff login token carries no `customerId`; internal register still works and creates no `Customers` row |
| Unit (domain) | PJ-11 — `SurveyResponse` valid/invalid ratings, free-text cap, null free text |
| Backend unit/archive | PJ-4 — `LoginCommandHandler`/`RegisterCommandHandler` claim/flag behaviour with the existing handler-test scaffolds |
| Frontend component (Vitest + `HttpTestingController`) | PJ-13..16 — form validation blocks, exact request bodies/URLs, success: navigation/message/reload/hide, empty states, claim-less body |
| Contract (shared spec) | the `portal.api.spec.ts` matrix mirrors `PortalController` routes |

Tests are named after the criterion (e.g. `PJ5_Submit_SetsSourcePortal`,
`PJ3_Login_TokenCarriesCustomerIdClaim`, `PJ16_AlreadySubmitted_HidesSurvey`).