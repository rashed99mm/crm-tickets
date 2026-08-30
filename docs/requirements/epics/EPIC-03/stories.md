# EPIC-03 · Communication Channels — Stories (All Slices)

| Epic | Slice(s) | BRD Requirements |
|---|---|---|
| `EPIC-03` | S3, S5, Deferred | FR-3.1–FR-3.9 |

---

## S5 — Message Recording (core prerequisite)

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| — | Create `Message` entity (TicketId, Direction, Channel, Subject, Body, SenderId, SentAt) | Backend | S5 | M | `not started` | FR-3.4 |
| — | Create message domain event (`MessageRecordedEvent`) | Backend | S5 | M | `not started` | FR-3.4 |
| — | EF migration for Messages table | Backend | S5 | M | `not started` | FR-3.4 |
| US-201 | Record inbound/outbound message against ticket | Backend | S5 | P0 | `not started` | FR-3.4 |
| — | `GetTicketMessagesQuery` (list messages per ticket, ordered) | Backend | S5 | M | `not started` | FR-3.4 |
| — | `MessagesController` on InternalApi (create + list) | Backend | S5 | M | `not started` | FR-3.4 |
| US-202 | Message timeline on ticket detail | Frontend | S5 | P0 | `not started` | FR-3.4 |

---

## S5 — Email Provider Integration

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-203 | Configure email provider integration | Backend | S5 | P0 | `not started` | FR-3.2, FR-3.3 |
| US-204 | Inbound email ingestion | Backend | S5 | P0 | `not started` | FR-3.2 |
| US-205 | Outbound email reply from ticket | Backend | S5 | P0 | `not started` | FR-3.3 |
| — | Idempotent inbound ingestion (deduplicate on provider message ID) | Backend | S5 | M | `not started` | INT-6 |
| — | Surface bounced/undeliverable mail to human | Backend | S5 | S | `not started` | FR-3.6 |
| — | Distinct inbound address per department | Backend | S5 | S | `not started` | FR-3.5 |

---

## S3 — Web Form Channel

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-404 | Customer Submits Ticket Through Portal | Backend | S3 | P0 | `not started` | FR-3.1 |
| — | Portal web form UI | Frontend | S3 | M | `not started` | FR-3.1 |

---

## Deferred (BRD §6.3)

| Story | Title | Status | Reason |
|---|---|---|---|
| FR-3.7 | WhatsApp two-way channel | Deferred | Paid provider, verified identity, no staffing |
| FR-3.8 | SMS outbound notifications | Deferred | Paid provider |
| FR-3.9 | Live chat with handover | Deferred | Real-time staffing required |

---

## Summary

| Slice | Total Stories | Done | Not Started |
|---|---|---|---|
| S3 | 2 | 0 | 2 |
| S5 | 12 | 0 | 12 |
| Deferred | 3 | — | — |
| **Total** | **14** | **0** | **14** |
