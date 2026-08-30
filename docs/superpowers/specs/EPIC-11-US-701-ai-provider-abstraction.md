# AI Provider Abstraction, Retrieval Quality and Multi-Turn Chatbot

**Date:** 2026-08-27
**Status:** Approved for implementation
**Type:** Enhancement of FEAT-21 (AI assist), backend + frontend
**Builds on:** `EPIC-11-US-701-feat-21-ai-assist-design.md`

## Problem

The AI feature works but binds to a single OpenAI-schema endpoint with a single model, no retry or
fallback, naive keyword retrieval, free-text category parsing, English-only prompts, and a
single-turn QA endpoint. That is not operable across providers nor good enough for real customers
(Arabic queries miss the knowledge base; a chatbot without conversation memory loses context on
the second message).

## Assumptions

- **A1.** `IAiService` (the five-operation port in `Application/Ai/IAiService.cs`) remains the
  Application boundary. Provider adapters live in Infrastructure only; feature handlers do not
  change shape except for error codes.
- **A2.** With no provider configured, the NoOp service registers and every surface degrades to
  the documented ERR052 "not configured" envelope, exactly as today.
- **A3.** API keys arrive from user-secrets or environment; they are never logged and never
  returned in any response.
- **A4.** The grounding sentinel `[NOT_IN_KB]` and the published-articles-only citation rule are
  unchanged.
- **A5.** Chat sessions are scoped to the actor: staff sessions on the InternalApi host (actor =
  staff user id), customer sessions on the ExternalApi host (actor = portal user id). A session
  row records its scope so an id from one scope can never be resolved through the other.

## Out of scope

Token streaming, embeddings/vector search, AWS Bedrock, WhatsApp integration with the chatbot,
per-tenant model routing.

## Acceptance criteria

### Provider abstraction and resilience

- **AI-30.** Given `Ai:Active` names a provider, when any AI operation runs, then that adapter's
  wire format is used (OpenAI-compatible, Anthropic or Gemini) and the response is projected into
  the existing `AiOutcome` vocabulary.
- **AI-31.** Given a provider returns 429/5xx or times out, when the call is attempted, then it
  retries with backoff (at most 3 attempts) and, on continued failure, fails over to the next
  provider in `Ai:Fallbacks` before surfacing an error.
- **AI-32.** Given all providers fail, when the feature responds, then the response carries
  `AI_PROVIDER_FAILED` (or `AI_RATE_LIMITED` when the last failure was a rate limit) with the
  reason — never the generic internal error.
- **AI-33.** Given a provider fails repeatedly, when later calls arrive, then a circuit breaker
  skips that provider for a cooldown window and the fallback serves the request.
- **AI-34.** Given any provider call, when logs are inspected, then token usage and latency are
  recorded and no API key, prompt PII or provider response body appears.

### Retrieval and prompting

- **AI-35.** Given an English or Arabic question, when retrieval runs, then BM25-style scoring
  (tokenization, stop-words, Arabic normalization, title boost) selects the top published
  articles; an empty result still yields the documented ungrounded refusal.
- **AI-36.** Given category or solution generation, when the model answers, then the output is
  schema-told JSON, schema-validated, and re-projected through the existing allow-list; malformed
  output is a safe failure, never garbage data.
- **AI-37.** Given the caller's locale, when prompts are built, then system prompts are
  localized (ar/en), knowledge-base and thread bodies are wrapped as delimited untrusted data,
  and optional `Ai:PiiScrub` patterns are applied before dispatch.

### Multi-turn chatbot

- **AI-38.** Given an authenticated actor, when a chat session starts, then a persisted session
  and first message are created and the session id returned.
- **AI-39.** Given an existing session, when a message is sent, then prior history informs the
  answer, retrieval re-runs, the turn is persisted with citations, and the grounded answer (or the
  QA001 refusal) is returned.
- **AI-40.** Given a session owned by another actor or of another scope or unknown, when it is
  accessed, then the response is a safe not-found that does not reveal existence.
- **AI-41.** Given any chat or ask route, when it is called, then the `"ai"` rate-limit policy
  applies, with a tighter per-IP window on the external host.
- **AI-42.** Given a staff session, when handoff is requested, then a ticket is created from the
  conversation and the session is closed.
- **AI-43.** Given a portal-authenticated customer, when they chat on the ExternalApi host, then
  the session is bound to their user id and scope and the same grounding contract applies.
- **AI-44.** Given an anonymous caller on ExternalApi, when a chat route is hit, then the response
  is `401` and nothing is persisted.
- **AI-45.** Given a portal session that ends with handoff, when the ticket is created, then it
  reaches the staff queue through the existing customer ticket path with the transcript as its
  description.
- **AI-47.** Given the frontend, when a user opens the assistant, then they can hold a multi-turn
  conversation with visible citations and degraded-mode and ungrounded states rendered distinctly.
