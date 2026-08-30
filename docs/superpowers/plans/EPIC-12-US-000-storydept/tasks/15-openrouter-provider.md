# Task 15 — OpenRouter AI provider (FEAT-21) — SHIPPED AS CONFIGURATION

## Traceability
Epic:   docs/requirements/epics/EPIC-11-ai.md
Stories: US-701-ai-provider-config.md … US-708-ai-suggestion-tracking.md
FEAT:   FEAT-21 — delivery-plan.md row 15 (was "gated on legal decision" — resolved: OpenRouter)

## Finding (verified 2026-08-27)
NO new code is needed. The provider factory (Infrastructure/Ai/AiProviderFactory.cs:187) already
defaults to the OpenAI-compatible adapter, and AiOptions.ConfigureLegacyFallback
(Application/Common/Options/AiOptions.cs) already defaults BaseUrl to
`https://openrouter.ai/api/v1` and Model to `meta-llama/llama-3.3-70b-instruct:free`.
The resilient chain (retry/backoff, breaker, PII scrub, usage logging, NoOp when unconfigured)
is fully implemented. With no key present the platform safely stays NoOp (A2).

## Ship step (deployment configuration — key NEVER in appsettings/git)
dotnet user-secrets set "Ai:ApiKey" "<openrouter-key>" --project src/CustomerSupport.InternalApi
dotnet user-secrets set "Ai:ApiKey" "<openrouter-key>" --project src/CustomerSupport.ExternalApi
# or environment: Ai__ApiKey=...   (BaseUrl/Model use the built-in OpenRouter defaults;
# override via Ai:BaseUrl / Ai:Model to switch models — e.g. a different free model.)

## Verification owed (recorded, not yet run — needs the key configured on a running host)
- One live smoke: ask the portal AI panel; confirm a real completion + token log line.
- US-703 human gate: nothing auto-sends; suggestions require agent action.

## Tests
Mocked-provider suites already in the tree cover retry/breaker/scrub; no live API in CI.
