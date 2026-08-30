# FEAT-21 · AI Assist — Stitch-Faithful Right Rail (Summary + Sentiment + Drafts + KB + Categories)

**Date:** 2026-08-28
**Status:** Approved (continuation of `EPIC-11-US-701-feat-21-ai-assist-design.md` and `EPIC-13-US-311-feat-21-frontend-design.md`)
**Type:** Amendment to FEAT-21 (vertical: backend payload shape + frontend right-rail restucture)
**Builds on:** Backend shipped per `EPIC-11-US-701-feat-21-ai-assist-design.md` and `EPIC-11-US-701-ai-provider-abstraction-design.md`. Frontend per `EPIC-13-US-311-feat-21-frontend-design.md`.
**Mockups referenced:** `stitch_smart_support_ticketing_crm/ai_powered_agent_workspace`, `ai_ticket_management_workspace`, `ticket_detail_chatbot`.

## Problem

The approved FEAT-21 spec defines five AI features and the QA behaviour. The backend is shipped.
The frontend is shipped but the right-rail layout on the ticket detail screen does not match the
three Stitch mockups the product owner cited: today the right rail is a single `<cs-card>` with
three trigger buttons and one generic pending-draft body. The mockups show a stacked rail of
**four** clearly-titled cards under one **"AI Assistant"** header — Context Summary, Suggested
Replies, Knowledge Base, Categories — with a sentiment chip on the summary card and an Insert
affordance on the suggested replies card that writes into the composer.

In addition, the existing payload shape stores a flat `text` for summaries and a `draft` string
for replies, but the product owner wants the Suggested Replies card to list several drafts at
once and the Summary card to carry a sentiment label.

## Assumptions

- **A1.** The current FEAT-21 envelope, error codes (`ERR052`, `AI_THREAD_TOO_SHORT`, `AI_PROVIDER_FAILED`) and degraded-mode behaviour are unchanged.
- **A2.** No new endpoints. The existing `POST api/Tickets/{id}/ai/{summary,categories,reply,solutions}` and the resolve/list commands are the surface. The change is in the **payload JSON** the handlers persist and the DTO returns.
- **A3.** The Draft-with-AI affordance lives in **both** the composer toolbar **and** the Suggested Replies card's "Insert" links. The card is the listing the mockups show; the toolbar is the keyboard-friendly fast path. Both write into the same composer body signal.
- **A4.** The right rail is **one shared "AI Assistant"** header band above the four cards. The four cards each title themselves. The `ERR052` first-answer rule from the previous frontend spec still applies to the whole rail.
- **A5.** Sentiment is generated as a separate lightweight call (cheap model hint) only when the summary operation succeeds, not as a separate user-triggered action. The system code for unparseable sentiment is "no sentiment" (`null`), never an error.
- **A6.** The mocked **Suggestions card with drafts** returns up to three drafts per call. The composer toolbar's Draft-with-AI uses the first draft; the card lists all three. If the model returns fewer, the UI renders what it gets.
- **A7.** Existing tests that cite `payload.draft` (singular string) and a summary payload as a plain string are updated to the new shape **without** dropping coverage. The `Kind` strings (`Summary`, `Reply`, `Categories`, `Solutions`) are unchanged.

## Out of scope

- The portal QA chatbot (`US-091`) — deferred indefinitely per BRD §6.3; the `AiChatController` work is unchanged.
- Auto-categorisation on intake (write-path automation).
- Streaming tokens, voice, embeddings, vector search.
- Multi-turn staff chat (`AI-38`…`AI-45`) — already shipped, lives in the portal and a separate spec.

## Acceptance criteria

Numbered AC-n cite the user stories' spec criteria or are epic additions:

### Backend (payload contract)

- **AC-21.11** *(epic — US-704 amendment)* — `POST .../ai/summary` returns `AiSuggestionDto` whose `Payload` is `{ "text": string, "sentiment": "Frustrated" | "Neutral" | "Satisfied" | null }`. Persisted `AiSuggestions.Payload` round-trips through the same shape.
- **AC-21.12** *(epic — US-706 amendment)* — `POST .../ai/reply` returns `AiSuggestionDto` whose `Payload` is `{ "drafts": string[] }`, with `drafts.length >= 1` and every entry a non-empty string. The composer toolbar's Draft-with-AI call uses `drafts[0]`; the right-rail card lists all `drafts`.
- **AC-21.13** *(epic — categories unchanged)* — `POST .../ai/categories` payload remains `{ options: { name: string }[] }`. Accepting a category still applies through `ticket.ApplySuggestedCategory` and resolves the suggestion.
- **AC-21.14** *(epic — solutions unchanged)* — `POST .../ai/solutions` payload remains `{ articles: { id, title }[] }`. Each article renders as a `routerLink` to `/knowledge-base/{id}`.
- **AC-21.15** *(epic — error path preserved)* — No-provider remains `503 ERR052`. Short thread remains `400 AI_THREAD_TOO_SHORT`. Provider failure remains `AI_PROVIDER_FAILED`. None of these change shape.
- **AC-21.16** *(epic — tracking preserved)* — `POST .../ai/suggestions/{id}` and `GET .../ai/suggestions` accept and return the new payload shapes; the tracking query's projection of `Payload` is `JsonElement` (unchanged) and clients continue to read it as a free-form object.

### Frontend (right rail)

- **AC-F9** *(epic — sentiment chip)* — When a summary's payload `sentiment` is one of the three enum values, the Summary card shows a chip with the matching icon (`sentiment_dissatisfied` / `sentiment_neutral` / `sentiment_satisfied`) and tinted background. A `null` sentiment renders no chip.
- **AC-F10** *(epic — drafts array + Insert)* — The Suggested Replies card lists every entry in `payload.drafts`. Each row has a hover-revealed "Insert" label. Clicking Insert on row N writes `drafts[N]` into the composer body signal, and clicking the composer toolbar's Draft-with-AI button writes `drafts[0]`. Both paths leave the existing `recordMessage` flow unchanged.
- **AC-F11** *(epic — AI Assistant header)* — The right rail renders a single header band with the `auto_awesome` glyph and the title `t('ai.title')` above the four cards. The header is chrome-free (not a `cs-card`).
- **AC-F12** *(epic — four-card layout)* — In render order: Context Summary, Suggested Replies, Knowledge Base, Categories. Each card titles itself. The `ERR052` first-answer rule from the previous frontend spec still applies to the whole rail.

### Frontend (composer integration)

- **AC-F13** *(epic — TicketMessagesComponent Draft-with-AI)* — The composer toolbar has a "Draft with AI" `cs-button` that calls `AiApi.draftReply(ticketId)` and writes the first draft into the body signal. The button is disabled while the call is in flight and hidden when `available()` is false (i.e. the `ERR052` rule already flipped the rail off).
- **AC-F14** *(epic — insertDraft API)* — `TicketMessagesComponent` exposes a public `insertDraft(text: string)` method. The right-rail card's Insert buttons call it via a parent/child signal handshake (no shared service). The method writes to the body signal, clears any field error, and does not auto-send.

### Cross-cutting

- **AC-F15** *(gates)* — `dotnet test` green; `ng test admin-app --watch=false` green; `ng build admin-app` clean. RTL-safe, no hardcoded strings — every new string goes through `| t` with the new `ai.*` keys.

## Design

### Backend payload shape

`AiSuggestionDto` stays as today (`Id, Kind, Payload, Status, Edited`). The `Payload` `JsonElement` becomes:

- `Kind == "Summary"`: `{ "text": string, "sentiment": "Frustrated" | "Neutral" | "Satisfied" | null }`
- `Kind == "Reply"`: `{ "drafts": [string, ...] }` (length 1..3, lower bound 1, upper bound 3 enforced in handler)
- `Kind == "Categories"`: unchanged `{ "options": [{ "name": string }, ...] }`
- `Kind == "Solutions"`: unchanged `{ "articles": [{ "id": string (Guid as string), "title": string }, ...] }`

### Backend handler changes

- `SummariseTicketCommandHandler`: call the existing `IAiService.SummariseAsync`, then a second `IAiService.ClassifySentimentAsync(threadText, ct)` (new port method, see `IAiService`). Persist `{ text, sentiment }` as the payload. The second call uses a cheaper, low-token prompt and degrades to `null` on any provider error, never failing the summary.
- `DraftReplyCommandHandler`: ask the model for **3** drafts in one call by changing the prompt to return JSON shaped `{ "items": [string, string, string] }` (re-use the existing `AiJson.ParseStringArray` schema), then de-duplicate, trim, drop empties, cap to 3. Persist `{ drafts: [...] }`.
- `IAiService` grows one method: `Task<AiOutcome<string?>> ClassifySentimentAsync(string threadText, CancellationToken ct)` — a three-way classification prompt. The NoOp implementation returns `Fail`. Features translate `Fail` into `null` and continue (A5).
- `ResilientAiService` implements `ClassifySentimentAsync` with a system prompt that asks the model to answer one of `Frustrated`, `Neutral`, `Satisfied` and parses strictly. Malformed output is `null`, not a 500.
- `NoOpAiService` implements `ClassifySentimentAsync` as `Fail` — the summary still succeeds with `sentiment: null`.

### Frontend component shape

- Replace `AiPanelComponent` body with a chrome-free header (`auto_awesome` + `t('ai.title')`) and four `<cs-card>`s. Each card holds the existing `cs-button` trigger for its kind, an `AsyncState<...>` for the load result, and a pending-draft body that renders whatever shape the payload has.
- New helper `draftsFromPayload()` reads `payload.drafts`; `sentimentFromPayload()` reads `payload.sentiment`; both tolerate legacy shapes (string fallback for the previous singular draft).
- Sentiment chip: `cs-badge` styled as the chip in `ai_ticket_management_workspace` — `error-container` for `Frustrated`, `surface-container` for `Neutral`, `tertiary-container` (or the brand's positive container) for `Satisfied`.
- `TicketMessagesComponent` exposes `insertDraft(text: string)`. `AiPanelComponent` declares an `insert` `output()` and the parent (`TicketDetailComponent`) wires it to the messages component's `insertDraft` (template reference variable). No service.
- Composer toolbar: a "Draft with AI" `cs-button` between the existing `Submit` and the body field. Disabled while `saving() || drafting()`. The `drafting` signal flips true during the call and back when it returns.

### i18n

New keys (en + ar, full bilingual):

| Key | en | ar |
|---|---|---|
| `ai.title` | AI Assistant | مساعد الذكاء الاصطناعي |
| `ai.summary` | Summarise | تلخيص |
| `ai.suggestedReplies` | Suggested Replies | ردود مقترحة |
| `ai.knowledgeBase` | Knowledge Base | قاعدة المعرفة |
| `ai.relatedArticles` | Related Articles | مقالات ذات صلة |
| `ai.noDrafts` | No drafts available | لا توجد مسودات |
| `ai.insert` | Insert | إدراج |
| `ai.draftWithAi` | Draft with AI | مسودة بالذكاء الاصطناعي |
| `ai.sentiment.frustrated` | Frustrated | محبط |
| `ai.sentiment.neutral` | Neutral | محايد |
| `ai.sentiment.satisfied` | Satisfied | راضٍ |
| `ai.edited` | (existing) edited | (existing) تم التعديل |

### Risks

- **Schema change without migration.** `AiSuggestions.Payload` is `NVARCHAR(MAX)` with a JSON shape. The new shape is additive (text + sentiment; drafts as an array; categories/solutions unchanged). No migration; existing rows continue to read, but the frontend treats unknown shapes via the `draftsFromPayload` / `sentimentFromPayload` helpers' fallback path.
- **Sentiment second call adds latency.** Bounded by the existing 20 s timeout. On a slow model the whole summarise operation can take 2× the timeout. AC-21.15 keeps `AI_PROVIDER_FAILED` if either call fails outright; sentiment failure alone falls through to `null` and does not fail the summary.
- **Insert into composer is a cross-component write.** Handled by parent/child signal handshake between `TicketMessagesComponent` and `AiPanelComponent`. No shared service — the surfaces are unrelated beyond this one handshake.
- **Free model quality.** Sentiment is a single-word answer; a misclassification is a chip of the wrong colour, not a wrong summary. Drafts are 1..3 strings the agent will edit; degraded models are bounded by the editor on the next screen.
