# AI Provider Abstraction & Multi-Turn Chatbot — plan record

**Spec:** [`../../specs/EPIC-11-US-701-ai-provider-abstraction-design.md`](../../specs/EPIC-11-US-701-ai-provider-abstraction-design.md)
**Status:** Implemented

## Task status

| Task | Criteria | Status | Evidence |
|---|---|---|---|
| 01 Provider port + multi-provider options | AI-30, A2 | done | `IAiProvider.cs`, `AiOptions.cs` rewrite with legacy flat-key back-compat |
| 02 OpenAI-compatible adapter | AI-30 | done | `Infrastructure/Ai/Providers/OpenAiCompatibleProvider.cs` (OpenAI, Azure, OpenRouter, Groq, Mistral, Ollama) |
| 03 Anthropic + Gemini adapters | AI-30 | done | `AnthropicProvider.cs` (Messages API), `GeminiProvider.cs` (generateContent) |
| 04 Factory, retry, breaker, fallback, codes | AI-31..33 | done | `AiProviderFactory.cs`; `ERR070/071/072` wired through SystemCode/Map/Resources + 503 mapping |
| 05 BM25-style bilingual retrieval | AI-35 | done | `KbRetriever.cs`; `AskKnowledgeBaseCommandHandler` switched to it |
| 06 Structured JSON + localized prompts + guard | AI-36, AI-37 | done | `AiJson`, ar/en prompts from `IUserContext.Locale`, `<untrusted_data>` fences, `Ai:PiiScrub` |
| 07 Chat domain + EF + migration | AI-38 | done | `AiChatSession`/`AiChatMessage` (Scope enum), migration `20260827144420_AddAiChat` (creates only; Down drops) |
| 08 Chat features + handoff | AI-38..40, AI-42, AI-46 | done | `Features/Ai/Chat/AiChatFeatures.cs` — staff handoff via `CreateTicketCommand`; portal handoff resolves Customer by email |
| 09 Hosts, scope, rate limits | AI-41..45 | done | `AiChatController` on InternalApi (`ai`, 30/min/IP) + ExternalApi (`ai-external`, 10/min/IP); scope set by host, never client |
| 10 Frontend | AI-47 | done | `chat.api.ts` + `ai-chat-panel.component.*` (t-pipe, RTL-safe), admin route `/ai-assistant` + sidebar, portal `/app/assistant` |

## Evidence (actual runs, 2026-08-27)

```
dotnet test CustomerSupport.slnx
Failed! - Failed: 14, Passed: 504, Skipped: 0, Total: 518

dotnet test --filter ~AiProviderAbstraction|~AiChatEndpointTests|~PortalAiChatEndpointTests|~AiAssistEndpointTests
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5

dotnet build CustomerSupport.slnx --warnaserror
Build succeeded. 0 Error(s)

ng test common  → Test Files 39 passed (39)
ng test admin-app → Test Files 26 passed (26)
ng test portal-app → Test Files 11 passed (11)
ng build admin-app / portal-app → Application bundle generation complete
```

The 14 remaining backend failures are the pre-existing, unrelated suites (Permissions, Portal
register, Content FAQ, Inbound channels, WhatsApp, SLA tracking) owned by concurrent work; none
touch this feature. Two pre-existing compile blockers were repaired en route because they broke
the build outright: `WhatsAppOutboundReplyTests` (missing using) and `RealtimeService.on` /
`chat-queue.component` type errors — minimal, convention-following fixes recorded here.
