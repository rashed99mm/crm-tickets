# CLAUDE.md - CommandCenter CMS Integration Gateway Mock Server

This file provides guidance to Claude Code when working with the CCE Integration Gateway Mock Server project.

## Project Overview

**CommandCenter CMS Integration Gateway Mock Server** is a Node.js/Express mock server that simulates external integration services (ERP, SMS, Email, and WhatsApp) for CommandCenter CRM.

**Port:** `3001`

**Purpose:** Enable local development and QC testing without depending on real external services.

## Quick Commands

```bash
# Install dependencies
npm install

# Start server (port 3001)
npm start

# Start with auto-reload (development)
npm run dev
```

**URLs:**
- Server: http://localhost:3001
- Swagger Docs: http://localhost:3001/api-docs
- Mock Manager UI: http://localhost:3001/mock-manager
- Health Check: http://localhost:3001/health

## Project Structure

```
ce-integration-gateway-mocks/
├── server.js              # Main Express server entry point
├── config.js              # Server configuration (PORT=3001)
├── swagger.js             # Swagger/OpenAPI configuration
├── routes.json            # URL rewriting rules for json-server
├── models/                # Declarative service definitions
│   ├── ServiceRegistry.js # Central registry
│   ├── SmsGatewayModel.js # SMS service schema & behavior
│   └── EmailGatewayModel.js # Email service schema & behavior
├── behaviors/             # Custom success/failure rules
│   ├── sms-rules.js       # SMS failure rules
│   └── email-rules.js     # Email failure rules
├── middlewares/           # Custom middleware
│   ├── gateway-handler.js # Generic middleware (reads from models)
│   ├── request-logger.js  # Request logging
│   ├── error-handler.js   # Global error handling
│   └── mock-admin/        # Mock Manager API (CRUD)
├── mocks/                 # JSON mock data files
│   ├── sms/
│   └── email/
├── scripts/               # Utility scripts
└── public/                # Frontend UI pages
```

## Architecture

### How Mock Data Works

1. **Mock JSON files** in `/mocks/` contain test data organized by service
2. **json-server** loads these files and provides automatic REST endpoints
3. **Custom middlewares** intercept requests to add business logic (validation, transformation, behavior rules)
4. **Models** (`models/*.js`) declare services declaratively — no hardcoded routes
5. **Routes** (`routes.json`) map external API paths to internal mock endpoints

### Service Naming Convention

Mock services follow the pattern `{group}-{name}`:
- `sms-templates` → `/mocks/sms/templates.json`
- `email-history` → `/mocks/email/history.json`

### Adding New Services

1. **Create model**: Add `models/NewServiceModel.js` with endpoint definitions
2. **Register**: Add `register(newModel)` in `models/ServiceRegistry.js`
3. **Create behavior** (optional): Add `behaviors/new-rules.js`
4. **Add mock data**: Create JSON files in `/mocks/newservice/`
5. **Restart server** — routes auto-wire from the model!

Example model structure:
```javascript
// models/my-service.js
module.exports = {
    name: 'my-service',
    group: 'integrationgateway',
    endpoints: [
        {
            path: '/integrationgateway/my-service/action',
            method: 'POST',
            mockDataKey: 'my-service-data',
            behaviorKey: 'my-rules',
            responseTransform: (req, mockData, rules) => {
                // Custom logic here
                return { success: true, data: mockData };
            }
        }
    ]
};
```

## Behavior Rules Engine

Rules are functions that decide if a request should succeed or fail:

```javascript
// behaviors/my-rules.js
module.exports = {
    check: (payload) => {
        if (payload.someField === 'bad-value') {
            return { code: 'ERROR_CODE', message: 'Error description' };
        }
        return null; // success
    }
};
```

## Mock Manager (QC Data Management)

The Mock Manager provides a web UI for QC teams to manage test data without server restarts.

### API Endpoints (`/api/mock-admin/*`)

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/services` | GET | List all mock services |
| `/services/:name` | GET | Get full service data |
| `/services/:name` | PUT | Update entire service |
| `/services/:name/records` | POST | Add new record |
| `/services/:name/records/:index` | PUT | Update specific record |
| `/services/:name/records/:index` | DELETE | Delete specific record |
| `/reload` | POST | Hot-reload mocks from disk |
| `/backup` | POST | Create manual backup |

### Hot-Reload Mechanism

Changes made via Mock Manager are:
1. Written to disk (atomic write for safety)
2. Updated in memory (`globalMocks`)
3. json-server router is recreated dynamically

No server restart required!

## Current Services

### SMS Gateway
- `POST /integrationgateway/sms/send` — Send SMS
- `GET /integrationgateway/sms/status/:messageId` — Check status
- `GET /integrationgateway/sms/templates` — List templates

### Email Gateway
- `POST /integrationgateway/email/send` — Send email
- `GET /integrationgateway/email/status/:messageId` — Check status
- `GET /integrationgateway/email/templates` — List templates

### KAPSARC Gateway (Circular Carbon Economy Index)
- `GET /integrationgateway/kapsarc/classification?countryCode=&countryName=&year=` — One country's reading
- `GET /integrationgateway/kapsarc/classifications?year=` — The full index for one edition (~680 KB)

Serves the **published** CCE Index: 125 countries × 5 editions (2021–2025), a 46-node indicator
tree, and every per-country/per-indicator reading. `classifications` also returns `availableYears`,
so a consumer refreshing everything discovers the other editions from its first call rather than
needing a separate lookup.

`classification` is returned as `null` on purpose — KAPSARC publishes numeric scores only, and the
backend derives the band from its own configured thresholds. Emitting a label here would silently
override that.

**Data files are generated, not hand-edited.** Regenerate all four from the backend repo:

```bash
python backend/docs/data/kapsarc/extract.py \
  --emit-mock /path/to/cce-integration-gateway-mocks/mocks/kapsarc
```

| File | Rows |
|------|------|
| `mocks/kapsarc/indicators.json` | 46 indicator-tree nodes |
| `mocks/kapsarc/country-facts.json` | 126 countries — demographics and groupings |
| `mocks/kapsarc/index-editions.json` | 628 country-editions — scores and ranks |
| `mocks/kapsarc/indicator-readings.json` | 28,888 readings |

`mocks/kapsarc/classifications.json` is a **different dataset** — the curated 22-country CCE set,
whose `totalIndex` is a 1–22 standing rather than a 0–100 score. Nothing above replaces it.

### Provider-faithful channel mocks (FEAT-35)

Impersonate the real vendor contracts so the backend's `Channels:UseMocks` flag is a base-URL swap
rather than a second code path.

- `POST /mock/sendgrid/v3/mail/send` — SendGrid v3. Answers `202` with an empty body and an
  `X-Message-Id` header.
- `POST /mock/meta/v18.0/:phoneNumberId/messages` — Meta Cloud API. Answers `200` with
  `messages[0].id` as a `wamid.`.
- `POST /mock/twilio/2010-04-01/Accounts/:accountSid/Messages.json` — Twilio, **form-encoded**.
  Answers `201` with a `sid`.

Deterministic failure triggers (`behaviors/provider-failure-rules.js`):

| Recipient | Behaviour |
|---|---|
| `permanent-fail@mock.test`, `+19995550000` | permanent `4xx` — never retried by the backend |
| `transient-fail@mock.test`, `+19995550001` | `503` twice, then success — exercises the bounded retry |

A model may answer with a status code and headers by returning
`{ $response: true, status, headers, body }`; returning a plain object keeps the historical
`200 + JSON`.

### Inbound simulators (FEAT-35 plan 2)

The gateway plays the provider for inbound too: these post to the **backend**
(`CALLBACK_BASE_URL`, default `http://localhost:5095`), they are not routes this server serves.

| Command | What it sends |
|---|---|
| `npm run simulate:sms` | Twilio-shaped form post to `/api/channels/sms/webhook`, signed with `WEBHOOK_SECRET` using Twilio's HMAC-SHA1-over-URL-plus-sorted-params scheme. Expects `200`. |
| `npm run simulate:sms -- --unsigned` | The same without the signature header. Expects `401` (CC-41). |
| `npm run simulate:email` | SendGrid Inbound Parse-shaped `multipart/form-data` post to `/api/channels/email/webhook`. Unsigned by design — Inbound Parse does not sign. Expects `200`. |
| `npm run simulate:email -- --twice` | The same payload twice with one `Message-ID`, which must store exactly one message (CC-43). |

`WEBHOOK_SECRET` here and `Channels__MockWebhookSecret` on the API host must match, or the signed
SMS post is refused with `401`.

The ExternalApi host also needs `Messaging__Required=false` locally unless RabbitMQ credentials are
configured, or it fails at startup before it ever listens.

## Development Guidelines

### When Adding a New External Service Mock

1. Study the real API documentation
2. Create mock data that covers happy path + edge cases
3. Implement a model for any dynamic behavior
4. Add route mapping if needed
5. Document in Swagger (add JSDoc to model file)

### When QC Needs New Test Data

Direct them to Mock Manager UI (`/mock-manager`):
- Select the service
- Use "Add Record" to create new test data
- Changes persist immediately

### Security Notes

- This server is for **local development only**
- No authentication on Mock Manager (intentional for dev convenience)
- Never expose to public networks
- Contains fake/test data only

## Common Tasks

### Find where an endpoint is handled
```bash
# Search for route pattern
grep -r "endpoint-path" models/ middlewares/
```

### Check what mock data exists for a service
```bash
cat mocks/{group}/{name}.json | head -50
```

### Manually reload mocks without restart
```bash
curl -X POST http://localhost:3001/api/mock-admin/reload
```

### Create a backup before testing
```bash
curl -X POST http://localhost:3001/api/mock-admin/backup
```

## Port Reference

- **Default Port:** `3001`
- **Set via:** `PORT` environment variable or `config.js`
- **Main AZM Mock Server** (if co-located): Port `3000`
