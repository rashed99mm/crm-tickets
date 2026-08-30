# Product vision

Sourced from [`../assessment/brief.md`](../assessment/brief.md) (verbatim client brief) and the
BRD's business objectives ([`../brd/customer-support-crm-brd.md`](../brd/customer-support-crm-brd.md)
§4). This file adds no goals of its own; where it summarises, the source is named and the source
wins.

## Product goal

The Customer Support CRM enables support teams to manage customers, receive and track support
requests, communicate with customers through supported channels, meet SLA targets, collaborate on
tickets, and measure support performance. *(brief, verbatim intent)*

The client's brief describes **twelve feature areas constituting a complete commercial support
platform** — a multi-team, multi-quarter product, not a single deliverable. Per assumption `B1`, it
is read as the *product vision*: the assessment delivers one vertical slice of it end to end
(slice S1 — ticket lifecycle), plus a documented decomposition of the remainder.

## Initial business outcomes

From the rule specification §1, restated against our delivery slices:

1. Agents create, receive, manage, and resolve support tickets — **S1**
2. Every ticket has a clear customer, owner, priority, status, and history — **S1**
3. Agents access relevant customer information and interaction history — **S1** (notes,
   attachments, customer summary; cross-ticket timeline deferred to later slices)
4. SLA response and resolution deadlines are calculated automatically — **S2**
5. Managers monitor ticket and SLA performance — **S6**
6. Administrators manage users, roles, permissions, and configuration — **partly S1** (auth, two
   roles, per-record authorization), remainder proposed as S9 (`G-2`)
7. Arabic and English, web and mobile-friendly interfaces — **S8**, with the bilingual message
   foundation already laid in S1 (`BR-22`, ADR 0007)

## Business objectives

Measured objectives live in the BRD §4 (`BO-1`–`BO-9`), each naming the KPI that measures it. The
two that S1 alone can move:

| Objective | Target |
|---|---|
| `BO-1` One system of record for customers and requests | ≥ 95% of known contacts recorded as tickets (`KPI-15`) |
| `BO-7` Every change attributable and auditable | 100% of state changes carry actor + UTC timestamp, enforced by the data model |

## What this product is not

Recorded so the boundary reads as decisions rather than omissions: not multi-tenant (`B4`),
not native mobile ("web and mobile friendly" = responsive web), not AI-dependent for core CRM
workflow (`B5`, rule spec §17.1). WhatsApp, SMS, live chat, ERP connectors and the AI chatbot are
deferred indefinitely with stated reasons (BRD §6.3).
