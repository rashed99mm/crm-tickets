# FEAT-22 Customer portal journey — Implementation Plan

**Spec:** `docs/superpowers/specs/EPIC-07-US-404-portal-journey-design.md`
**Epic:** `EPIC-07 Customer portal`
**Sprint:** `10 — Customer portal` · Slice S3
**Predecessor:** `EPIC-07-US-404-portal-home-and-signup` (shipped — portal public home, signup, login, `/app` guard)
**Status:** planned; application implementation not started

> **Test policy for this feature.** The user has directed that suites are not run until all tasks in the
> programme of work are complete; task "Run" lines below therefore describe what will be executed and what must
> pass, and the **final task** runs the build and the focused portal/OTP filters (and the full suite if the
> parallel feature's failing tests have landed). No test result is claimed until it has actually been run.

## Existing patterns (cited)

- Registration: `backend/src/CustomerSupport.Application/Features/Auth/Commands/Register/RegisterCommandHandler.cs` (lines 57–79: `ApplicationUser.Create`, `user.PhoneNumber`, `CreateAsync`, role assignment)
- Claims: `backend/src/CustomerSupport.Infrastructure/Security/TokenService.cs` (line 20 `GenerateAccessToken(..., additionalClaims)`, lines 40–43 claim merge); call sites `Login/LoginCommandHandler.cs:70`, `RefreshToken/RefreshTokenCommandHandler.cs:83`
- Customer + unit-of-work: `backend/src/CustomerSupport.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandHandler.cs` (whole — `IRepository<Customer>`, `IUnitOfWork`, `IDbExceptionTranslator`, unique-violation → conflict)
- Ticket create: `Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs` (lines 47–59) and `Ticket.SetSource` at `Domain/Entities/Tickets/Ticket.cs:335`
- Message: `Domain/Entities/Tickets/TicketMessage.cs` (line 17 `AllowedChannels`, `Create` at 36–91)
- Envelope/codes: `Application/Messages/SystemCodeMap.cs`, `Application/Errors/ApplicationErrors.cs`, `Api.Shared/Localization/Resources.yaml`, `Api.Shared/Extensions/ResponseExtensions.cs`
- External host: `CustomerSupport.ExternalApi/Controllers/AuthController.cs` (Register at 47–67), `CustomerSupport.ExternalApi/Program.cs`

## Contract summary

- `RegisterCommand` gains `bool IsPortalRegistration = false`; ExternalApi passes `true`.
- `ApplicationUser.CustomerId` (`Guid?`), set via `LinkCustomer(Guid)`.
- JWT gains `customerId` claim on login/refresh only when the user has a link.
- `CreateTicketCommand` gains `string? Source` (validated; `"Portal"` sets `Ticket.Source`).
- `TicketMessage.AllowedChannels` gains `"Portal"`.
- New `SurveyResponse` entity (`TicketId` unique, `Rating` 1–5, `FreeText` ≤ 2000).
- New ExternalApi surface: `POST/GET api/portal/tickets`, `GET api/portal/tickets/{id}`, `POST api/portal/tickets/{id}/reply`, `POST api/portal/tickets/{id}/survey`, `GET api/Categories`.
- New shared client `PortalApi` (common lib) + portal-app rewires off `TicketApi`.

## Tasks

### Task 1 — Domain & persistence foundation (US-401/408, PJ-1/2/11)

**Files:** `Domain/Entities/Identity/ApplicationUser.cs`, `Domain/Entities/Survey/SurveyResponse.cs` (new),
`Infrastructure/Persistence/AppDbContext.cs` (identity + entity config wiring),
`Infrastructure/Persistence/Configurations/SurveyResponseConfiguration.cs` (new), migration.

**Steps:**
1. `ApplicationUser`: add `Guid? CustomerId` + `void LinkCustomer(Guid customerId)` (throws on empty; no general setter).
2. `SurveyResponse : BaseEntity, IAppendOnlyEntity` with static `Create(Guid ticketId, int rating, string? freeText)` — rating 1..5 and freeText ≤ 2000 throw; null freeText allowed.
3. Configure: unique index on `SurveyResponses.TicketId`; required columns; `AspNetUsers.CustomerId` nullable FK → `Customers.Id`.
4. Migration `AddCustomerLinkAndSurvey`.

**Domain tests (write first):** `SurveyResponse_ValidRating_Accepted`, `..._RatingBelow1_Throws`, `..._RatingAbove5_Throws`, `..._FreeTextOptional`, `..._FreeTextTooLong_Throws`, `ApplicationUser_LinkCustomer_Set`, `..._Empty_Throws`.

**Run:** `dotnet test --filter "FullyQualifiedName~SurveyResponse|FullyQualifiedName~ApplicationUser"` (from `backend/`) — domain boundaries proven.
**Commit:** `feat: add customer link and survey response domain model`

### Task 2 — Portal registration creates the customer record (US-401, PJ-1/2)

**Files:** `Application/Features/Auth/Commands/Register/RegisterCommand.cs`,
`.../Register/RegisterCommandHandler.cs`, `ExternalApi/Controllers/AuthController.cs`,
`Application/Errors/ApplicationErrors.cs` (reuse `Customer.EMAIL_EXISTS`).

**Steps:**
1. `RegisterCommand`: append `bool IsPortalRegistration = false`.
2. `RegisterCommandHandler`: inject `IRepository<Customer>`, `IUnitOfWork`, `IDbExceptionTranslator`; when the flag is set — `Customer.Create($"{FirstName} {LastName}".Trim(), Email, PhoneNumber)`, `await customers.AddAsync(customer)`, `user.LinkCustomer(customer.Id)` then the existing `CreateAsync`. Catch unique-violation on that save → `Customer.EMAIL_EXISTS` conflict (A4 — the customer row flushes inside identity's single `SaveChanges`, so it is atomic).
3. `ExternalApi AuthController.Register`: pass `IsPortalRegistration: true`. InternalApi unchanged.
4. Add `RegisterCommandHandlerTests` cases: portal flag persists a `Customers` row + `CustomerId` link; no flag → no customer record (regression).

**Run:** `dotnet test --filter "FullyQualifiedName~RegisterCommandHandler"`.
**Commit:** `feat: create customer record on portal registration`

### Task 3 — `customerId` claim on login/refresh (US-402/403, PJ-3/4)

**Files:** `Application/Features/Auth/AuthClaimTypes.cs` (new), `.../Login/LoginCommandHandler.cs`,
`.../RefreshToken/RefreshTokenCommandHandler.cs`.

**Steps:**
1. `AuthClaimTypes`: `public const string CustomerId = "customerId";`
2. Both handlers: build `user.CustomerId is { } cid ? [new Claim(AuthClaimTypes.CustomerId, cid.ToString())] : null` and pass as `additionalClaims`. Staff (no link) stays `null`.

**Tests:** login issues claim when linked; claim absent for staff; refresh re-issues the claim.
**Run:** `dotnet test --filter "FullyQualifiedName~LoginCommandHandler|FullyQualifiedName~RefreshTokenCommandHandler"`.
**Commit:** `feat: issue customerId claim on auth token`

### Task 4 — Portal ticket create with `Source="Portal"` (US-404, PJ-5/6)

**Files:** `Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommand.cs`,
`.../CreateTicketCommandHandler.cs`, `.../CreateTicketCommandValidator.cs`,
`Application/Errors/ApplicationErrors.cs` (new `Source`/`Portal` codes).

**Steps:**
1. `CreateTicketCommand`: append `string? Source`.
2. Handler: after `Ticket.Create(...)`, `if (request.Source is { } source) ticket.SetSource(source);` (staff pass null → unchanged).
3. Validator: when `Source` present, must be one of `Portal, WebForm, WhatsApp, SMS, Email, LiveChat` → new `Validation.SOURCE_INVALID`.
4. Existing handler tests still green; add a source-set case.

**Run:** `dotnet test --filter "FullyQualifiedName~CreateTicket"`.
**Commit:** `feat: allow portal source on ticket creation`

### Task 5 — Portal queries: own tickets + detail (US-403/405/406, PJ-7/8/9)

**Files:** `Application/Features/Portal/Dtos/PortalDtos.cs` (new), `.../Queries/GetPortalTickets/*` (new),
`.../Queries/GetPortalTicketDetail/*` (new), `Infrastructure/Populate`/claims helper shared by handlers.

**Steps:**
1. DTOs: `PortalTicketListItemDto(Id, Reference, Subject, Status, CreatedAt)`; `PortalMessageDto(Direction, Body, SentAt)`; `PortalTicketDetailDto(Id, Reference, Subject, Description, Status, Priority, CreatedAt, Messages, SurveySubmitted)`.
2. `GetPortalTicketsQuery(Guid CustomerId)` — `ListProjectedOrderedAsync(t => t.CustomerId == claim, t => t.CreatedAt, descending: true, ...)`.
3. `GetPortalTicketDetailQuery(Guid TicketId, Guid CustomerId)` — missing → `Ticket.NOT_FOUND` 404; owned-mismatch → `General.FORBIDDEN` 403; messages via `IRepository<TicketMessage>.ListOrderedAsync(m => m.TicketId == id, m => m.SentAt, ...)`; `surveySubmitted` via `IRepository<SurveyResponse>.ExistsAsync(r => r.TicketId == id)`.
4. Claim helper `PortalClaim.ForbiddenIfMissing(value)` in the controller layer (Task 7).

**Tests:** list filters to owner; detail includes history + survey flag; other-customer → 403; unknown → 404. Written as handler unit tests with an EF in-memory style fake only where the existing `IRepository` test patterns do (prefer the integration tests in Task 8 for behaviour).
**Run:** `dotnet test --filter "FullyQualifiedName~Portal"` (after Task 8).
**Commit:** `feat: portal ticket list and detail queries`

### Task 6 — Portal reply + survey command (US-407/408/409, PJ-10/11/12)

**Files:** `Domain/Entities/Tickets/TicketMessage.cs` (AllowedChannels += `"Portal"`),
`Application/Features/Portal/Commands/CreatePortalReply/*` (new),
`Application/Features/Portal/Commands/SubmitSurvey/*` (new),
`Application/Errors/ApplicationErrors.cs` (new `Survey` codes), `Messages/SystemCode.cs` +
`SystemCodeMap.cs` + `Resources.yaml` + `ResponseExtensions.cs` (wire `SURVEY_ALREADY_SUBMITTED` → 409,
`SURVEY_TICKET_NOT_RESOLVED` → 400, `PORTAL_FORBIDDEN`/`General.FORBIDDEN` → 403).

**Steps:**
1. `CreatePortalReplyCommand(TicketId, Body)`; handler: load ticket → 404/403, `TicketMessage.Create(ticketId, "Inbound", "Portal", null, body, userContext.UserId)`, add + save; validator: `MESSAGE_BODY_REQUIRED`.
2. `SubmitSurveyCommand(TicketId, Rating, Comment)`; handler: ownership → 404/403; status must be `Resolved`/`Closed` else `Survey.TICKET_NOT_RESOLVED`; `ExistsAsync` → `Survey.ALREADY_SUBMITTED` 409; `SurveyResponse.Create`, add + save, success `SURVEY_SUBMITTED`. Validator: rating inclusive 1..5 → `SURVEY_RATING_REQUIRED/SURVEY_RATING_INVALID`, comment ≤ 2000.
3. Wire the new codes through `SystemCode`, `SystemCodeMap`, `Resources.yaml` (en/ar), and `ResponseExtensions`.

**Tests:** reply blocking matrix (own/403/404/400), survey matrix (201/400 unresolved/409 dup/403/400 rating).
**Run:** `dotnet test --filter "FullyQualifiedName~Portal"`.
**Commit:** `feat: portal reply and survey submission`

### Task 7 — ExternalApi surface: PortalController + CategoriesController (US-404..409, PJ-5..12)

**Files:** `CustomerSupport.ExternalApi/Controllers/PortalController.cs` (new),
`CustomerSupport.ExternalApi/Controllers/CategoriesController.cs` (new).

**Steps:**
1. `PortalController` (`[Authorize]`, `[Route("api/portal")]`): read `customerId` claim via `User.GetClaim(AuthClaimTypes.CustomerId)` → convert; absent → 403 `FORBIDDEN_ACCESS`.
   - `POST tickets`: resolve `CreateTicketCommand` (CustomerId = claim, `Source = "Portal"`, body fields) → 201.
   - `GET tickets`: own list → 200.
   - `GET tickets/{id}`: detail → 200 (403 when `TICKET_FORBIDDEN`, 404).
   - `POST tickets/{id}/reply` → 201.
   - `POST tickets/{id}/survey` → 201.
2. `CategoriesController` (`[Authorize]`, `GET api/Categories`): wraps existing `GetCategoriesQueryHandler` → the portal submit picker.
3. Integration tests over the **external** host (`CrmApiFactory`- or external `WebApplicationFactory`): register → assert `Customers` row + link; login → assert `customerId` claim; PJ-5/6/7/8/9/10/12 full matrices. Internal-host integration: staff register/login produce no `Customers` row and no claim (PJ-4).

**Run:** `dotnet test --filter "FullyQualifiedName~Portal"` (backend, includes Task 5/6 unit + Task 7 integration).
**Commit:** `feat: expose customer portal endpoints on external host`

### Task 8 — Frontend shared client `PortalApi` (PJ-13..16 contract)

**Files:** `frontend/projects/common/src/lib/portal/portal.api.ts` (new), `portal.api.spec.ts` (new), common `index.ts` exports.

**Steps:** `listTickets`, `getTicket(id)`, `submitTicket({subject, description, categoryId, priority})`, `reply(id, body)`, `submitSurvey(id, {rating, comment})`, `listCategories`; typed DTO interfaces mirroring Task 5; spec matrix checks exact routes/bodies (no customerId in the submit payload).
**Run:** `cd frontend && npx ng test common --watch=false`.
**Commit:** `feat: add customer portal api client`

### Task 9 — Portal screens rewire: submit + my-tickets (US-411/412, PJ-13/14)

**Files:** `portal-app/src/app/features/tickets/submit.component.{ts,html,spec.ts}`,
`.../list.component.{ts,html,spec.ts}`, `portal-app/translations` (via common `translations.ts`) keys.

**Steps:**
1. Submit: drop `CustomerOption`/`customers`/`searchCustomers`; category picker via `PortalApi.listCategories`; submit via `PortalApi.submitTicket` (no customerId); success → `/app/tickets`. Client validation blocks empty subject/description/category with `expectNone`.
2. List: unpaged `PortalApi.listTickets`; status pill + reference per row; empty state; row → `/app/tickets/{id}`.
3. Add en/ar keys `portal.tickets.submit.*` and `portal.tickets.list.*`.

**Run:** `cd frontend && npx ng test portal-app --watch=false`.
**Commit:** `feat: rewire portal submit and my-tickets screens`

### Task 10 — Portal detail + reply + survey screens (US-413/414/415, PJ-15/16)

**Files:** `portal-app/src/app/features/tickets/detail.component.{ts,html,spec.ts}`,
`.../reply-form` and `.../survey-form` inline or component files as the template dictates, translations.

**Steps:**
1. Detail loads `PortalApi.getTicket(id)` by route param; renders subject, reference, status pill, description, and the `messages` timeline oldest-first.
2. Reply form (inline): shown when status ≠ `Closed`; non-empty required; on success clear + reload detail; server/validation errors on the control.
3. Survey card: shown when `surveySubmitted === false` and status ∈ {Resolved, Closed}; rating 1–5 buttons + optional comment; no rating blocks submit client-side; success → `surveySubmitted=true`, thank-you state (PJ-16 AC3).

**Run:** `cd frontend && npx ng test portal-app --watch=false`.
**Commit:** `feat: portal ticket detail with reply and survey`

### Task 11 — Verify: build and test gate

**Files:** story statuses (`docs/requirements/user-stories/US-401..US-415.md`), delivery-plan row 10, traceability doc.

**Steps:**
1. `cd backend && dotnet build CustomerSupport.slnx` clean.
2. Focused: `dotnet test --filter "FullyQualifiedName~Portal|FullyQualifiedName~Register|FullyQualifiedName~Login|FullyQualifiedName~SurveyResponse|FullyQualifiedName~CreateTicket"`.
3. `cd frontend && npx ng build common && npx ng test common --watch=false && npx ng test portal-app --watch=false`.
4. If the parallel feature's 14 failing tests have landed: full `dotnet test` + `npx ng test admin-app`; otherwise record the shared-suite caveat in the delivery plan, plainly.
5. Flip story statuses to shipped only for what the run proved; update delivery-plan row 10 and traceability.

**Commit:** `test: evidence customer portal journey behaviour`