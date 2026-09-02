# CommandCenter CMS - Integration Gateway Mock Server

> **Port: `3001`**

A reusable mock server for CommandCenter CMS integration gateway services (ERP, SMS, Email, WhatsApp, and more).

## Quick Start

```bash
# Install dependencies
npm install

# Start the server (runs on port 3001)
npm start

# Start with auto-reload (development)
npm run dev

# Start on a different port
PORT=3002 npm start
```

## URLs

| URL | Description |
|-----|-------------|
| `http://localhost:3001` | Dashboard Landing Page |
| `http://localhost:3001/api-docs` | Swagger UI Documentation |
| `http://localhost:3001/mock-manager` | Web UI for Managing Mock Data |
| `http://localhost:3001/health` | Health Check Endpoint |
| `http://localhost:3001/api/server-info` | Server Status & Info |

## Port Configuration

The default port is **`3001`**. You can change it via:

1. **Environment variable:** `PORT=3002 npm start`
2. **`.env` file:** Create a `.env` file with `PORT=3002`
3. **`config.js`:** Edit the `PORT` constant directly

## Project Structure

```
cms-integration-gateway/
├── server.js                          # Express entry point
├── config.js                          # Server configuration (port, paths, etc.)
├── package.json                       # Dependencies & scripts
├── swagger.js                         # OpenAPI/Swagger specification
├── routes.json                        # URL rewrite rules
│
├── models/                            # Service Registry - declarative definitions
│   ├── ServiceRegistry.js             # Central registry
│   ├── SmsGatewayModel.js             # SMS service model
│   └── EmailGatewayModel.js           # Email service model
│
├── behaviors/                         # Success/failure rule engine
│   ├── sms-rules.js                   # SMS failure rules
│   └── email-rules.js                 # Email failure rules
│
├── middlewares/
│   ├── gateway-handler.js             # Auto-wires models to routes
│   ├── request-logger.js              # Request logging
│   ├── error-handler.js               # Global error handling
│   └── mock-admin/                    # Full CRUD admin API
│       ├── index.js
│       └── utils.js
│
├── mocks/                             # JSON mock data files
│   ├── sms/
│   │   ├── responses.json             # Predefined responses
│   │   ├── templates.json             # SMS templates
│   │   └── history.json               # Message history
│   └── email/
│       ├── responses.json
│       ├── templates.json
│       └── history.json
│
├── scripts/                           # Utility scripts
│   ├── start.bat                      # Windows start script
│   ├── start.sh                       # Linux/Mac start script
│   ├── test-endpoints.ps1             # PowerShell endpoint tests
│   ├── test-endpoints.sh              # Bash endpoint tests
│   └── backup-mocks.ps1               # Backup mock data
│
├── public/                            # Frontend UI
│   ├── index.html                     # Dashboard landing page
│   └── mock-manager.html              # Mock data management UI
│
└── .env.example                       # Environment variable template
```

## Available Services

### SMS Gateway

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/integrationgateway/sms/send` | Send SMS message |
| `GET` | `/integrationgateway/sms/status/:messageId` | Check delivery status |
| `GET` | `/integrationgateway/sms/templates` | List SMS templates |

### Email Gateway

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/integrationgateway/email/send` | Send email |
| `GET` | `/integrationgateway/email/status/:messageId` | Check delivery status |
| `GET` | `/integrationgateway/email/templates` | List email templates |

### KAPSARC Gateway (Circular Carbon Economy Index)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/integrationgateway/kapsarc/classification` | One country's index reading (`countryCode`, `countryName`, optional `year`) |
| `GET` | `/integrationgateway/kapsarc/classifications` | Full index for one edition (optional `year`) — catalog, countries, facts and readings |

Serves the published CCE Index: 125 countries × 5 editions (2021–2025), a 46-node indicator tree
and 28,888 readings. One edition is ~680 KB; the response carries `availableYears` so a consumer
refreshing everything can loop the remaining editions without a separate discovery call.

Data files under `mocks/kapsarc/` are **generated** — regenerate with
`python backend/docs/data/kapsarc/extract.py --emit-mock <this repo>/mocks/kapsarc`.

### CMS ERP Gateway

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/integrationgateway/erp/tickets` | Return sample ERP tickets for CMS import testing |

The Internal API consumes this feed through `POST /api/integrations/cms/erp/import-tickets`.

## Behavior Rules (Custom Failures)

The mock server includes configurable behavior rules for testing error scenarios:

### SMS Rules
| Trigger | Error Code |
|---------|------------|
| Phone number ends in `000` | `INVALID_PHONE_NUMBER` |
| Phone number contains `999` | `CARRIER_BLOCKED` |
| Missing `+` country code prefix | `MISSING_COUNTRY_CODE` |

### Email Rules
| Trigger | Error Code |
|---------|------------|
| Starts with `bounce@` | `BOUNCE` |
| Contains `spam` | `SPAM_DETECTED` |
| Missing `@` or `.` | `INVALID_EMAIL` |

## Mock Admin API

Manage mock data without restarting the server:

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/mock-admin/services` | `GET` | List all services |
| `/api/mock-admin/services/:name` | `GET` | Get service data |
| `/api/mock-admin/services/:name` | `PUT` | Update service |
| `/api/mock-admin/services/:name/records` | `POST` | Add record |
| `/api/mock-admin/services/:name/records/:index` | `PUT` | Update record |
| `/api/mock-admin/services/:name/records/:index` | `DELETE` | Delete record |
| `/api/mock-admin/reload` | `POST` | Hot-reload from disk |
| `/api/mock-admin/backup` | `POST` | Create manual backup |

## NPM Scripts

```bash
# Start the server
npm start

# Start with auto-reload (requires nodemon)
npm run dev

# Test all endpoints
npm test

# Test SMS endpoints only
npm run test:sms

# Test Email endpoints only
npm run test:email

# Create a backup of mock data
npm run backup

# Reload mocks from disk
npm run reload
```

## Adding a New Service

1. **Create the model** in `models/` (e.g., `PushGatewayModel.js`)
2. **Register it** in `models/ServiceRegistry.js`:
   ```javascript
   const pushModel = require('./PushGatewayModel');
   register(pushModel);
   ```
3. **Create behavior rules** in `behaviors/push-rules.js` (optional)
4. **Add mock data** in `mocks/push/*.json`
5. **Restart the server** — routes auto-wire!

## Environment Variables

Copy `.env.example` to `.env` and configure:

```bash
PORT=3001
NODE_ENV=development
LOG_LEVEL=info
CORS_ENABLED=true
```

## Sample API Calls

### Send SMS (Success)
```bash
curl -X POST http://localhost:3001/integrationgateway/sms/send \
  -H "Content-Type: application/json" \
  -d '{"to":"+966501234567","from":"CCE-Carbon","body":"Hello from CCE Carbon!"}'
```

### Send SMS (Failure - Invalid Number)
```bash
curl -X POST http://localhost:3001/integrationgateway/sms/send \
  -H "Content-Type: application/json" \
  -d '{"to":"+966500000000","body":"Test"}'
```

### Send Email
```bash
curl -X POST http://localhost:3001/integrationgateway/email/send \
  -H "Content-Type: application/json" \
  -d '{"to":"user@example.com","subject":"Welcome","html":"<h1>Welcome to CCE Carbon</h1>"}'
```

### Get Templates
```bash
curl http://localhost:3001/integrationgateway/sms/templates
curl http://localhost:3001/integrationgateway/email/templates
```

## License

Internal use only — CCE Carbon.
