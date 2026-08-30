# Integrations

External systems touch this product only through adapters at defined boundaries (architecture
principle 17.2) — never embedded in domain logic. Core CRM workflows must function with every
adapter absent (principle 17.1: core CRM first).

## Current state

The CMS integration boundary is now live for the local ERP mock. The admin Integrations page calls
`POST /api/integrations/cms/erp/import-tickets`; the Internal API fetches
`GET /integrationgateway/erp/tickets`, creates missing customers and tickets tagged `CMS-ERP`, and
skips an existing `externalId` on repeat imports. The mock remains replaceable through
`Integrations:Cms:ErpBaseUrl`.

## Planned channels

| Channel | Sprint | Hard blockers / open questions | Notes |
|---|---|---|---|
| Inbound email → ticket | 9 | `DEP-1` (provider choice) — hard blocker; `OQ-11` (matching rule: how an inbound message attaches to an existing customer/ticket) | First real adapter; defines the port shape others follow |
| WhatsApp | Deferred | BRD §6.3 deferral; provider choice (`OQ-1` in the rule-file discovery register) | Same adapter port as email where possible |
| SMS | Deferred | BRD §6.3 deferral | Notification-class only |
| ERP connectors | CMS mock implemented | Production ERP choice remains `OQ-7` | Read-mostly ticket import proof of concept |
| AI assist | 15 | `OQ-8` — legal/compliance gate before any technical work | Adapter keeps the core independent whether or not AI ever ships |
| Customer portal | 10 | Depends on conversation record (sprint 6) and email channel (sprint 9) | Not an integration per se but consumes the same boundaries |

## Resilience contract (applies from the first adapter onward)

Per `NFR-INT-001`, each adapter specification must define — before implementation:

1. **Retry behaviour** — bounded retries with backoff for transient failures.
2. **Failure recording** — failed inbound/outbound messages are persisted, never dropped silently.
3. **Monitoring** — failures surface somewhere an operator looks.
4. **Manual retry** — a recorded failure can be re-driven by a person.
5. **Idempotency** — where a redelivery could double-apply (email matching is the obvious case),
   the operation is safe to repeat.

These five points are owed per-adapter at their sprint; defining them globally now would be
speculation. What *is* decided globally: an integration failure must never corrupt CRM data, and
must never block the core workflows — the CRM stays usable with every adapter down.

## Boundary mechanics

Adapters implement ports owned by the Application layer (the same pattern the attachment storage
port uses — `NFR-18`). Infrastructure contains the adapter implementations; Domain and Application
never reference provider SDKs. Consequence: swapping the email provider after `DEP-1` resolves is a
new adapter class, not a domain refactor.
