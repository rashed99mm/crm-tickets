# Task 06 - AI Experience Completion

**Status:** In progress  
**Closes gaps:** Suggested reply button, static chat AI sidebar, AI KB article 404.

## Files

- Backend: `AiController.cs`, `AiChatController.cs`, `KnowledgeBaseAiController.cs`
- Frontend API: `common/src/lib/ai/*`
- Frontend UI: `tickets/ai-panel.component.*`, `chat/chat-session.component.*`

## Implementation

- Keep ticket AI as source of truth for linked ticket sessions.
- Add chat AI suggest-reply endpoint for unlinked chat sessions.
- Ensure AI citations include route-safe article ids and target surface.
- Fix admin KB article links to existing route.
- Add unavailable state for `ERR052`.

## Code Example

```csharp
[HttpPost("chats/{sessionId:guid}/suggest-reply")]
public Task<IActionResult> SuggestChatReply(Guid sessionId, CancellationToken ct) =>
    Send(new SuggestChatReplyCommand(sessionId), ct);
```

```html
<cs-button variant="secondary" (pressed)="suggestReplies()">
  <cs-icon name="reply" [size]="16" />
  {{ 'ai.draftReply' | t }}
</cs-button>
```

## Acceptance

- [x] Suggested Reply button calls backend.
- [x] Draft insertion targets the active composer.
- [x] Chat AI sidebar is API-backed when possible.
- [ ] KB citation links do not 404.
- [x] AI disabled state is visible and non-blocking.

## Evidence

- Added `POST /api/chat/sessions/{sessionId}/ai/reply`, backed by the existing `IAiService.DraftReplyAsync` provider port and rate-limited with the staff AI policy.
- Added `ChatReplySuggestionDto`, `ChatApi.suggestReply`, and chat workspace loading/error/empty/draft states.
- Removed local static quick replies from `chat-session.component.ts`; generated drafts now arrive from the API and insert into the active composer.
- Verified `npx ng build admin-app` passed.
- Verified focused admin chat test: `npx ng test admin-app --watch=false --include=projects/admin-app/src/app/features/chat/chat-session.component.spec.ts` passed 2 tests, including API-backed draft insertion.
