# Customer Support CRM

Customer Support CRM is a bilingual customer-support platform with an agent/admin workspace, a customer portal, ticket lifecycle management, SLA automation, notifications, reporting, knowledge-base management, live chat, and integration mocks.

The repository contains two .NET 10 API hosts and two Angular applications. Both frontends share a common Angular library for API clients, authentication, localization, state, UI components, notifications, reports, and portal contracts.

## Product Areas

- Admin CRM: dashboard, ticket queue and detail workspace, customers, agent workspace, chat, knowledge base, reports, users, permissions, audit logs, settings, and integrations.
- Customer portal: public landing pages, sign-up/sign-in, FAQ and knowledge base, ticket submission, ticket list/detail, replies, attachments, surveys, live chat, notifications, and customer profile/history.
- Automation: five-minute SLA scanning, warning notifications, escalation handling, and status-change notifications.
- Integrations: CMS/ERP mock gateway plus email, SMS, WhatsApp, and external-system integration boundaries.
- Localization: English and Arabic UI with runtime language switching and RTL support.

## Repository Layout

```text
backend/
  src/
    CustomerSupport.Domain/          Domain entities and rules
    CustomerSupport.Application/     CQRS commands, queries, validators, DTOs
    CustomerSupport.Infrastructure/  EF Core, repositories, jobs, integrations
    CustomerSupport.Api.Shared/      Shared API middleware and response handling
    CustomerSupport.InternalApi/     Staff/admin API host
    CustomerSupport.ExternalApi/     Customer portal API host
    CustomerSupport.Migrator/        Database migration/seed host
    CustomerSupport.Shared.Contracts/Shared contracts
  tests/CustomerSupport.Tests/       Unit and integration tests
frontend/
  projects/common/                   Shared Angular library
  projects/admin-app/                Admin CRM application
  projects/portal-app/               Customer portal application
  e2e/                               Playwright browser tests
cms-integration-gateway/             Node.js mock integration gateway
docs/                                BRD, SDD, ADRs, architecture, requirements, plans
stitch_smart_support_ticketing_crm/  UI reference/mockup material
```

## Architecture

The backend follows Clean Architecture with CQRS:

```text
Controller -> MediatR command/query -> Handler -> Repository/unit of work -> Database
```

- Controllers are thin: authentication checks, request binding, MediatR dispatch, and response mapping only.
- Application handlers contain use-case orchestration and depend on interfaces, not infrastructure implementations.
- Domain entities enforce lifecycle and business rules.
- Infrastructure implements persistence, identity, background jobs, notifications, and external clients.
- API responses use the shared bilingual response envelope with success/code/message/data/errors/traceId/timestamp.
- Frontend API clients consume the unwrapped `data` payload through the shared envelope interceptor.

## System Flow Catalog

How a request actually travels, end to end. Each flow names the real entry point and the files that
carry it, so a reader can follow one path without reconstructing it from the directory tree.

### 1. Ticket capture — three doors, one lifecycle

| Door | Entry point | Who | Notes |
|---|---|---|---|
| Staff create | `POST /api/Tickets` (`InternalApi/Controllers/TicketsController.cs`) | authenticated agent | Chooses the customer on someone's behalf; takes `impact` + `urgency`. |
| Customer portal | `POST /api/portal/tickets` (`ExternalApi/Controllers/PortalController.cs`) | signed-in customer | No `customerId` in the body — it comes from the session (`PJ-8`). No priority either: customer-origin tickets do not self-classify (`US-923`), the server derives it. |
| Public web form | `POST /api/external/webform/submit` (`ExternalApi/Controllers/WebFormController.cs`) | anonymous visitor | Honeypot + per-IP window; both refusals answer exactly like a success so a bot learns nothing (`CC-47`). |

All three converge on `Ticket.Create`, which assigns a `TKT-nnnnnn` reference from a SQL sequence
(`Infrastructure/Persistence/TicketReferenceGenerator.cs`). `NEXT VALUE FOR` sits outside the
caller's transaction, so a rolled-back create burns a number rather than reissuing one.

### 2. Inbound channel ingestion — provider payload in, ticket message out

```text
provider webhook -> channel controller (parse + verify) -> IngestInboundChannelMessageCommand
                 -> resolve/create Customer -> resolve/create open Ticket for (customer, channel)
                 -> append TicketMessage
```

| Channel | Route | Authenticity |
|---|---|---|
| WhatsApp | `POST /api/channels/whatsapp/webhook` | `X-Hub-Signature-256` — HMAC-SHA256 over the **raw body** (`MetaSignatureVerifier`) |
| SMS | `POST /api/channels/sms/webhook` | `X-Twilio-Signature` — HMAC-SHA1, Base64, over the **request URL plus ordinal-sorted form parameters** (`TwilioSignatureVerifier`) |
| Email | `POST /api/channels/email/webhook` | **None** — SendGrid Inbound Parse does not sign its posts; the payload authenticates nothing beyond what it claims |
| Web form | `POST /api/external/webform/submit` | Honeypot + rate window, not a signature |

Both signed channels are verified **before any database write**, and both verifiers sit behind one
`IWebhookSignatureVerifier` port via `CompositeWebhookSignatureVerifier`, which dispatches on the
provider name each verifier already gates on. Adding a provider adds an implementation, not a branch.

One open ticket per `(customer, channel)`: a non-terminal ticket receives the message, a terminal one
starts a new ticket. A retried delivery carrying a provider message id already seen is a no-op
success, not a duplicate row — the webhook still gets `200`, because a failed ingestion is not
something the provider should redeliver for hours.

### 3. Outbound reply — agent to customer, over the customer's own channel

```text
POST /api/Tickets/{id}/messages -> RecordTicketMessageCommandHandler
   -> TicketMessage (Outbound) -> INotificationGateway -> channel sender -> provider
```

The contact field is chosen per channel and never both: phone channels carry `PhoneNumber`, email
carries `Email`. Email additionally skips `@channel.invalid` addresses — the placeholders minted for
phone-only customers to satisfy a non-nullable email column, which are not deliverable.

The three HTTP senders share `ChannelHttpSender` (transport, auth, bounded retry, result mapping) and
own only their payload shape and id/error mapping: SendGrid v3 for email, Meta Cloud API for
WhatsApp, Twilio's form-encoded contract for SMS.

### 4. The mock/real toggle — a decorator, not a second code path

Every sender and the inbound verifiers read their base URL and credential through
`IExternalApiConfigurationProvider` and nothing else. `Channels:UseMocks` therefore needs one
decorator — `MockRoutingExternalApiConfigurationProvider` — to point the three channel gateways at
the local mock server. No sender, handler or controller knows mocks exist. Startup **fails** if the
flag is ever true under `Production`: a mock that accepts and discards customer notifications is
worse than an outage, because every send reports success.

Credentials arrive already decrypted at the provider boundary; nothing downstream unprotects them
again.

### 5. Live chat — anonymous, session-scoped, real time

```text
POST /api/external/chat/start -> session + opaque token
   -> SignalR /hubs/chat joined with that token (no account, no bearer)
   -> agent claim -> messages both ways -> agent may convert the session to a ticket
```

### 6. SLA and escalation — the only clock in the system

A hosted scanner sweeps for breaches, sets escalation levels, appends history and notifies the
assignee. Business-hours arithmetic lives behind `IBusinessHoursCalculator`, so "four working hours"
does not silently mean four wall-clock hours.

### 7. Notifications — one gateway, many channels

`INotificationGateway` resolves the recipient's channels, renders the template, and dispatches
through the per-channel senders, recording a `NotificationDeliveries` row per attempt with the
provider's own message id. Transient provider failures retry within a bounded policy; permanent ones
never retry.

### 8. AI assist — grounded, and in the reader's language

Summaries, draft replies, suggested solutions and the knowledge-base "ask" all run through
`ResilientAiService`, which selects its **entire prompt** — English or Arabic — from
`IUserContext.Locale`. That locale is read from the `Accept-Language` header, which the frontend
sends from its own language store (`acceptLanguageInterceptor`). Without that header the server would
follow the *browser's* language and the UI's language switch would not reach the model.

### 9. Request/response envelope and localization

```text
Angular API client -> refresh -> auth -> accept-language -> HTTP
   -> server resolves message text in the requested language
   -> envelope { success, code, message, data, errors, traceId, timestamp }
   -> envelopeInterceptor unwraps to `data`, or throws a typed ApiError
```

Components therefore see plain typed models or an `ApiError` — never envelope fields. Domain enum
values (statuses, priorities) are deliberately **not** translated client-side; that would keep a
second copy of a server-owned vocabulary and blank any value the server later adds.

### 10. Attachments

Uploaded after the ticket exists, because the endpoint is keyed off a real ticket id: the form
secures the row first, then streams files at it, best-effort per file so one failure strands neither
the ticket nor the other files.

## Requirements

- .NET SDK 10
- Node.js and npm
- SQL Server for the default database configuration
- Optional local services: Redis and RabbitMQ
- A configured JWT signing key for authenticated API calls

## Configuration

Development configuration is in:

- `backend/src/CustomerSupport.InternalApi/appsettings.Development.json`
- `backend/src/CustomerSupport.ExternalApi/appsettings.Development.json`

Set environment variables, user secrets, or local configuration for values such as:

- `ConnectionStrings:DefaultConnection`
- `ConnectionStrings:Redis`
- `RabbitMQ:Username` and `RabbitMQ:Password`
- `Jwt:Key`
- AI and external provider API keys

Do not commit real credentials or provider keys. The checked-in settings use `__SET_...__` placeholders where a value must be supplied. The CMS/ERP mock defaults to `http://localhost:3001`.

## Run Locally

Run each process from a separate terminal.

### CMS/ERP mock gateway

```bash
cd cms-integration-gateway
npm install
npm start
```

Gateway URLs:

- Health: `http://localhost:3001/health`
- API docs: `http://localhost:3001/api-docs`
- Mock manager: `http://localhost:3001/mock-manager`

### Internal API

```bash
cd backend
dotnet run --project src/CustomerSupport.InternalApi --launch-profile CustomerSupport.InternalApi
```

Development URL: `http://localhost:5074`.

This host serves staff/admin operations, authentication, tickets, customers, reports, administration, notifications, and settings.

### External API

```bash
cd backend
dotnet run --project src/CustomerSupport.ExternalApi
```

Development URL: `http://localhost:5095`.

This host serves customer portal operations under `/api/portal`, including tickets, replies, surveys, attachments, and customer profile endpoints.

### Admin application

```bash
cd frontend
npm install
npm start -- --project admin-app --port 4200 --proxy-config proxy.conf.json
```

Open `http://localhost:4200`.

### Customer portal

```bash
cd frontend
npm start -- --project portal-app --port 4201 --proxy-config proxy.portal.conf.json
```

Open `http://localhost:4201`.

The proxy files route frontend `/api` calls to the appropriate local API host.

## Database

The application uses Entity Framework Core and SQL Server. Apply migrations with the migrator project after configuring the database connection:

```bash
cd backend
dotnet run --project src/CustomerSupport.Migrator
```

The exact migration/seed behavior is controlled by the migrator configuration and environment settings. Verify the database is reachable before starting API hosts.

## Testing and Verification

### Start the full development stack

From the repository root, run:

```bat
run-dev.cmd
```

This opens both .NET 10 APIs and both Angular apps with the configured proxies:

- Admin app: `http://localhost:4200` -> Internal API `http://localhost:5074`
- Portal app: `http://localhost:4201` -> External API `http://localhost:5095`

The script sets the development environment, local JWT values, and `Messaging__Required=false`.
Set `ConnectionStrings__DefaultConnection` before running to use another SQL Server database.

When `SeedData=true`, the internal API creates local development accounts for every role. The
seeded role accounts use password `Support@123456`; the existing administrator remains
`admin@cce-platform.com` with password `Admin@123456` for compatibility with the integration suite.

The full-access development account is `superadmin@support.local` with password
`Support@123456`. It can open the admin application, manage departments, users, permissions,
knowledge-base content, settings, reports, tickets, customers, and live chat. The backend remains
the authority for these permissions; the sidebar only hides links that the current role cannot use.

### Backend

```bash
cd backend
dotnet test
```

Build an individual API:

```bash
dotnet build src/CustomerSupport.InternalApi/CustomerSupport.InternalApi.csproj
dotnet build src/CustomerSupport.ExternalApi/CustomerSupport.ExternalApi.csproj
```

### Frontend unit tests

```bash
cd frontend
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
```

Build the applications separately because this is a multi-project Angular workspace:

```bash
npx ng build admin-app
npx ng build portal-app
```

Run a focused Angular suite:

```bash
npx ng test admin-app --watch=false --include=projects/admin-app/src/app/features/dashboard/dashboard.component.spec.ts
```

### End-to-end tests

```bash
cd frontend
npm run e2e
```

Start the APIs and frontends first when running browser tests against live services.

## Important API Routes

### Authentication

- `POST /api/Auth/login`
- `POST /api/Auth/register`
- `POST /api/Auth/refresh`
- `GET /api/Auth/me`
- `PUT /api/Auth/me`

### Admin/staff

- `GET /api/Tickets`
- `GET /api/Customers`
- `GET /api/reports/ticket-volume`
- `GET /api/reports/agent-performance`
- `GET /api/reports/sla-performance`
- `GET /api/Notifications`
- `POST /api/Notifications/{id}/read`

### Customer portal

- `GET /api/portal/tickets`
- `POST /api/portal/tickets`
- `GET /api/portal/tickets/{id}`
- `POST /api/portal/tickets/{id}/reply`
- `POST /api/portal/tickets/{id}/survey`
- `GET /api/portal/profile`
- `PUT /api/portal/profile`

Portal endpoints derive the customer identity from the authenticated token and do not accept arbitrary customer IDs from the browser.

## Background Automation

The SLA scanner runs using the `SlaAutomation` configuration:

```json
{
  "ScanIntervalMinutes": 5,
  "WarningPercentage": 0.8
}
```

The automation evaluates due tickets, creates warning/escalation notifications, and applies the configured escalation workflow. Every status transition is recorded in ticket history, and affected users receive notifications through the configured notification/realtime channels.

## Integrations

The integration page is available in the admin application under system settings. The CMS/ERP mock supports importing sample tickets through the backend integration boundary. The backend client is configured through:

```json
{
  "Integrations": {
    "Cms": {
      "ErpBaseUrl": "http://localhost:3001"
    }
  }
}
```

The mock gateway is for local development and demonstrations; replace it with real provider adapters and credentials for production.

## Documentation

- Business requirements: `docs/brd/`
- Architecture: `docs/architecture/`
- ADRs: `docs/adr/`
- Requirements and user stories: `docs/requirements/`
- SDD specs and implementation plans: `docs/superpowers/`
- UI references: `stitch_smart_support_ticketing_crm/`

The documentation uses the project epic/story naming convention such as `EPIC-08-US-606-...` for aligned specs and plans.

## Troubleshooting

- API returns connection-string errors: configure `ConnectionStrings:DefaultConnection` and verify SQL Server is running.
- API returns JWT errors: configure `Jwt:Key`, `Jwt:Issuer`, and `Jwt:Audience` consistently in both API hosts.
- Portal requests fail with CORS errors: confirm both `4200` and `4201` are in each API's development `Cors.AllowedOrigins` list.
- Reports reject `PageSize=200`: report and list APIs accept page sizes from `1` through `100`; the frontend should cap page size at `100`.
- CMS import fails: start `cms-integration-gateway` on port `3001` and verify `/health`.
- UI appears unchanged after a template update: stop and restart the relevant Angular dev server, then reload without a stale browser cache.

## Security Notes

- Client-side role checks only control visibility; backend authorization remains authoritative.
- Portal queries are scoped to the authenticated customer.
- Passwords, JWT keys, database credentials, and third-party API keys belong in environment variables or user secrets.
- Do not use the mock gateway or placeholder configuration as a production integration strategy.
