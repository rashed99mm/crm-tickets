---
name: security-and-edge-cases
description: Use when implementing auth, handling user input, exposing an endpoint, managing configuration or secrets, or reviewing a feature before it is called done - carries the authorization rules, secret handling rules and the edge-case checklist to work through
---

# Security and edge cases

## Overview

Two things that share one habit: asking "what happens when this goes wrong?" before someone else
asks it for you. Both are graded together, and both are where a feature that demos beautifully
falls apart.

## The trust boundary

**The server validates everything. Always.** Frontend validation is a convenience for the user;
it is not a control. Anyone can call the API directly with curl, and an attacker will.

- Never trust a client-supplied id for ownership. `GET /orders/123` must verify *this* user may
  see order 123 — an authenticated user is not an authorized one, and the gap between those two
  is the most common real vulnerability in CRUD applications.
- Never trust a client-supplied role, price, total or status. Recompute anything that matters
  server-side. A price sent from the browser is a suggestion.
- Bind explicitly to request DTOs. Binding straight to entities invites over-posting, where a
  caller sets `IsAdmin` or `Status` by adding a field to the JSON.

## Authentication and authorization

- Validate the JWT properly: signature, issuer, audience, expiry, and algorithm. Never accept
  `none`, and never take the algorithm from the token header alone.
- Short-lived access tokens. If refresh tokens are in scope, they are rotated and revocable.
- Authorize per resource, not only per endpoint. Endpoint-level `[Authorize]` proves the caller
  is *someone*; the handler still has to prove they may touch *this record*.
- **A 404 for a record that exists but is not yours** is usually better than a 403, which
  confirms the record exists.
- Enforce authorization server-side even where the UI hides the button. A hidden button is not a
  permission.

## Secrets and configuration

- **No secrets in source.** No connection strings, keys, or passwords in `appsettings.json`, in
  a committed `.env`, or in a test fixture. Once committed, a secret is compromised — rotating
  it is the only fix, and deleting the line does not undo it.
- Local development uses user secrets or an untracked file; `.env.example` documents the *shape*
  with placeholder values only.
- Never log secrets, tokens, or full request bodies containing credentials.

## Responses that leak

- No stack traces, SQL, or inner exception chains in a response body. Log them server-side with
  a correlation id and return the id.
- Do not let error messages distinguish "no such user" from "wrong password" — that turns a
  login form into an account enumerator.
- Serialise DTOs, never entities. Entities acquire fields, and a new column becomes a new
  disclosure without anyone deciding to publish it.

## Baseline hardening

- HTTPS, with HSTS in production.
- CORS restricted to known origins. `AllowAnyOrigin` together with credentials is both wrong and
  refused by browsers.
- Rate limit authentication endpoints and anything expensive. .NET has built-in rate limiting.
- Cap request body size and page size. Uncapped means a single request can exhaust memory.
- Parameterised queries only — which EF Core gives you unless you hand-write SQL. If you do write
  raw SQL, parameterise it; string interpolation there is an injection.
- Angular escapes interpolated values by default. **`bypassSecurityTrustHtml` re-opens XSS** —
  do not use it on anything that came from a user.

## Edge-case checklist

Work through this per feature before calling it done. Most items are one test each.

**Input**
- Empty string, whitespace-only, null where nullable
- Exactly at the length limit, and one over
- Zero, negative, and maximum numeric values
- Unicode, emoji, and right-to-left text in text fields
- Wrong type entirely (a string where a number is expected)

**Collections**
- Empty list — does the UI show an empty state or look broken?
- One item, and many
- Page beyond the last page
- `pageSize` of 0, of -1, and of a million

**State and identity**
- A record that does not exist
- A record belonging to another user
- A record already in the target state (delete twice, cancel a cancelled order)
- Two callers racing the same record — does the second get a clear conflict, or silently
  overwrite the first?

**Failure**
- Database unreachable
- A dependency timing out
- A partial failure mid-operation — is the write atomic, or is there now half a record?

**Time**
- Boundaries at midnight and month end
- Timezone differences between client and server
- An expired token mid-session

## Red flags

| Thought | Reality |
|---|---|
| "The UI hides that button" | A hidden button is not a permission. Enforce it server-side. |
| "The user is logged in, so they can see it" | Authenticated is not authorized. Check per record. |
| "I'll put the key in appsettings for now" | Once committed it is compromised, and deleting the line does not undo it. |
| "Returning the exception message helps debugging" | It is an information-disclosure finding. Correlation id instead. |
| "Nobody would send a negative page size" | Someone will, and the stack trace will tell them about your database. |
| "Concurrency won't happen at this scale" | Two clicks on a slow connection is concurrency. |
| "I'll do the edge cases if there's time" | This is a graded criterion, and it is where thin work shows. |
