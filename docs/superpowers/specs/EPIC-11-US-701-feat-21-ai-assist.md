# FEAT-21 — AI Assist (free-model drafting assistant + grounded QA chatbot)

**Date:** 2026-08-26
**Status:** Draft for approval
**Type:** Vertical epic (backend + frontend)
**Stories:** US-701…US-708 (roadmap sprint 15, pulled forward by product decision 2026-08-26)
**Depends on:** Contents/KB read models (shipped), ticket detail screen (FEAT-06)

## Context

The brief's Area 7 promises AI assistance: ticket summaries, category suggestions, drafted replies,
suggested solutions, and an ask-the-KB experience. The delivery plan parked this at sprint 15,
gated on a legal decision; the product owner has directed it be built now against a **free AI
model**, which removes the cost objection that motivated the gate. Nothing in this epic sends data
to a paid processor or trains on our content beyond what the chosen provider's free tier already
permits.

Two distinct behaviours ship under one epic:

1. **Drafting assistant** (staff-side, US-703…706, 708): summaries, category suggestions and reply
   drafts for agents working a ticket. Everything is a *draft behind the existing human gate* —
   the agent confirms, edits or rejects before anything reaches a customer. The AI never sends.
2. **QA chatbot** (customer-side behaviour): question answering grounded strictly in published KB
   articles. Retrieval first, generation second; when retrieval finds nothing relevant the bot
   says so instead of improvising. It answers questions; it does not take actions, change tickets,
   or speak outside the knowledge base.

## Assumptions

- **A1 — Provider is OpenRouter-compatible with a free model.** One `HttpClient` implementation of
  the AI port speaks the OpenAI-style `/chat/completions` schema, base URL and model id come from
  configuration (`Ai:BaseUrl`, `Ai:Model`, default a `:free` model id such as
  `meta-llama/llama-3.3-70b-instruct:free`; exact default recorded in config, swappable without
  redeploy). API key via user-secrets/env. No SDK dependency — raw JSON, so any compatible gateway
  (OpenRouter today, others later) works.
- **A2 — Absent key degrades to NoOp, never to failure.** Mirroring the messaging arrangement:
  without credentials, suggestions endpoints return `success=false` with a documented
  "not configured" code and the UI hides affordances. The rest of the CRM keeps working.
- **A3 — Suggestion lifecycle is persisted, not ephemeral.** Every suggestion row stores its kind,
  payload, status (`Pending → Accepted | Rejected`), `edited` flag and actor — US-708's tracking,
  making acceptance-rate queryable later.
- **A4 — Grounding rule for the QA bot.** Answers may only be composed from retrieved published
  article text. Empty/irrelevant retrieval ⇒ explicit refusal message with its own system code
  (`QA001 Ungrounded`), never a hallucinated answer. Citations (article ids/titles) are returned
  alongside every answer.
- **A5 — Chat is anonymous-read like the KB it stands on.** The QA endpoint sits beside the
  external knowledge-base controller: read-only inputs, no writes, rate-limit friendly payloads.
  Staff drafting endpoints require auth as any internal endpoint.
- **A6 — Latency budget.** Free tiers queue; every AI call runs with a timeout (config, default
  20 s) and returns the standard error envelope on timeout rather than hanging a request.

## Acceptance criteria

Numbered AC-n cite the story files' spec criteria verbatim ranges:

- **AC-21.1** *(US-701)* — Provider settings live in configuration; no credential ever appears in a
  response body or log output.
- **AC-21.2** *(US-702)* — Application defines `IAiService` (single port); Infrastructure supplies
  the provider implementation; DI registration honours A2.
- **AC-21.3** *(US-703)* — All drafting outputs are drafts: creating a suggestion never mutates the
  ticket; only an agent confirm applies anything, and reject leaves state unchanged.
- **AC-21.4** *(US-704)* — Summarise endpoint returns a short summary only above a minimum thread
  size; short threads get a documented "nothing to summarise" result, and the UI shows the summary
  where the agent reads the thread.
- **AC-21.5** *(US-705)* — Category suggestion returns options drawn from real categories; accepted
  suggestion updates the ticket through the normal path.
- **AC-21.6** *(US-706)* — Draft-reply fills the composer as editable text; sending remains the
  existing record-message flow; a sent-after-AI-draft marks the suggestion consumed.
- **AC-21.7** *(US-707)* — Solution suggestions reference published KB articles with id+title+link;
  the sidebar opens the article in place.
- **AC-21.8** *(US-708)* — Status transitions enforced (`Pending → Accepted|Rejected` only);
  `edited` tracked on modification; tracking queryable by ticket.
- **AC-21.9** *(epic addition — QA behaviour)* — `POST api/knowledge-base/ask` retrieves top-k
  published articles, answers only from them, returns citations, refuses ungrounded answers with
  `QA001`, and never mutates data.
- **AC-21.10** *(contract continuity)* — Every new endpoint obeys the S1 envelope, stable codes,
  traceId, ISO/camelCase wire rules proven in the execution-proof epic.

## Out of scope

Auto-categorisation on intake (write-path automation), chat transcript storage, streaming tokens,
voice, training/fine-tuning, paid models, autonomous actions of any kind.
