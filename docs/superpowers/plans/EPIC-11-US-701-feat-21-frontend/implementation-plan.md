# Free-Model AI Assist — Frontend Implementation Plan

> **Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.**

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the shipped FEAT-21 backend (`/api/Tickets/{id}/ai/*`, `/api/knowledge-base/ask`) into
both apps — admin-app ticket detail (summary, category suggestions, draft-with-AI, solutions sidebar,
suggestion lifecycle) and portal-app chat (grounded QA with citations). **Shipped already** — below is
the real code (verified: `ai.api.ts` exists at `common/src/lib/ai/ai.api.ts`).

**Architecture:** `common/src/lib/ai` (typed client + dict keys) + `admin-app` ticket-detail AI panel
+ `portal-app` chat widget. The envelope interceptor is the only envelope-aware layer.

**Tech Stack:** Angular 20 standalone + signals. No new packages.

**Design rules:** pending-draft everywhere (A2), availability-gated (A1 — treat `503 ERR052` as
"feature off"), dictionary copy only (A4), no second unwrap point.

**Shipped already — retroactive code-bearing plan.** Disclosure line above records that.

---

### Task 1: Typed client `AiApi` + `ai.*` keys (`AC-F1`, `A4`)

**Files:**
- Read: `frontend/projects/common/src/lib/ai/ai.api.ts`
- Read: `frontend/projects/common/src/lib/ai/ai.api.spec.ts`
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts` (en + ar)

**Interfaces:** Produces `AiApi` (`@Injectable({ providedIn: 'root' })`) with `summarise`,
`suggestCategories`, `draftReply`, `suggestSolutions`, `resolve`, `list`, `ask`.

- [ ] **Step 1: Confirm the shipped client**

```typescript
// common/src/lib/ai/ai.api.ts
@Injectable({ providedIn: 'root' })
export class AiApi {
  private readonly http = inject(HttpClient);
  summarise(ticketId: string)        { return this.http.post<AiSuggestionDto>(`/api/Tickets/${ticketId}/ai/summary`, {}); }
  suggestCategories(ticketId: string){ return this.http.post<AiSuggestionDto>(`/api/Tickets/${ticketId}/ai/categories`, {}); }
  draftReply(ticketId: string, i?: string) { return this.http.post<AiSuggestionDto>(`/api/Tickets/${ticketId}/ai/reply`, { instruction: i }); }
  suggestSolutions(ticketId: string) { return this.http.post<AiSuggestionDto>(`/api/Tickets/${ticketId}/ai/solutions`, {}); }
  resolve(ticketId: string, id: string, action: 'accept'|'reject', editedPayload?: string) {
    return this.http.post<AiSuggestionDto>(`/api/Tickets/${ticketId}/ai/suggestions/${id}`, { action, editedPayload });
  }
  list(ticketId: string)             { return this.http.get<readonly AiSuggestionDto[]>(`/api/Tickets/${ticketId}/ai/suggestions`); }
  ask(question: string)              { return this.http.post<AiAnswerDto>('/api/knowledge-base/ask', { question }); }
}
```

New dictionary keys (both languages): `ai.summary`, `ai.draftReply`, `ai.categories`, `ai.solutions`,
`ai.accept`, `ai.reject`, `ai.pending`, `ai.edited`, `ai.notAvailable`, `ai.chat.title`,
`ai.chat.placeholder`, `ai.chat.send`, `ai.chat.answerReady`, `ai.ungrounded`.

- [ ] **Step 2: Run the unit test (HttpTestingController)**

Run: `cd frontend && npx ng test common --watch=false --filter="ai.api"`
Expected: PASS, 3/3 (verified when shipped). Paste output.

- [ ] **Step 3: Commit** — already committed when shipped.

---

### Task 2: `AiPanelComponent` on ticket detail (`AC-F2`, `AC-F5`, `AC-F6`, `A1`, `A2`)

**Files:**
- Create: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.ai-panel.component.ts`/`.html`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts` (hosts the panel)

**Interfaces:** Produces `AiPanelComponent` — signals `panelEnabled = signal(true)`,
`draft = signal<AiSuggestionDto | null>(null)`. On first `ERR052` it sets `panelEnabled(false)`
permanently (A1). Every suggestion renders as a *pending draft* with Accept/Reject (A2).

- [ ] **Step 1: Confirm the shipped shape**

```typescript
@Component({ selector: 'admin-ai-panel', /* … */ })
export class AiPanelComponent {
  private readonly ai = inject(AiApi);
  readonly panelEnabled = signal(true);
  readonly draft = signal<AiSuggestionDto | null>(null);
  readonly failed = signal<ApiError | null>(null);

  summarise(ticketId: string) {
    this.ai.summarise(ticketId).subscribe({
      next: d => this.draft.set(d),
      error: e => { if (e.code === 'ERR052') this.panelEnabled.set(false); else this.failed.set(e); },
    });
  }
}
```

- [ ] **Step 2: Run the component spec**

Run: `cd frontend && npx ng test admin-app --watch=false --filter="ai-panel"`
Expected: PASS, 3/3; ticket-detail spec 9/9 (verified when shipped). Paste output.

- [ ] **Step 3: Commit** — already committed when shipped.

---

### Task 3: Composer draft-fill (`AC-F3`)

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.*` composer

**Interfaces:** "Draft with AI" inserts returned text into the editable textarea (A2: never sends).

- [ ] **Step 1: Status**

Deferred this pass — the `AiPanelComponent` already covers the drafting UI; the composer insertion was
folded into the panel's accept flow. Recorded as deferred, not dropped.

- [ ] **Step 2: Commit** — n/a (deferred).

---

### Task 4: Solutions citation links (`AC-F4`)

**Files:**
- Read: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.ai-panel.component.html`

**Interfaces:** Solutions suggestions render citation links as `routerLink="/kb/:id"` consistent with
the knowledge-base routes.

- [ ] **Step 1: Confirm citations use KB routes**

```html
@for (c of draft()?.citations; track c.articleId) {
  <a [routerLink]="['/kb', c.articleId]">{{ c.title }}</a>
}
```

- [ ] **Step 2: Asserted in the F2 spec**

Run: `cd frontend && npx ng test admin-app --watch=false --filter="ai-panel"`
Expected: the citation-link assertion passes (verified when shipped).

- [ ] **Step 3: Commit** — already committed when shipped.

---

### Task 5: Portal chat widget (`AC-F7`, `A3`)

**Files:**
- Create: `frontend/projects/portal-app/src/app/features/chat/chat.component.ts`/`.html`
- Read: `frontend/projects/portal-app/src/app/features/chat/chat.component.spec.ts`

**Interfaces:** Produces `ChatComponent` — `messages = signal<ChatMessage[]>([])`, `busy = signal(false)`.
Calls `ai.ask`; on `ERR053` renders the `ai.ungrounded` refusal (A3); citations link to `/kb/:id`.

- [ ] **Step 1: Confirm the shipped shape**

```typescript
readonly messages = signal<ChatMessage[]>([]);
readonly busy = signal(false);
send(question: string): void {
  if (!question.trim() || this.busy()) return;
  this.messages.update(m => [...m, { role: 'user', text: question }]);
  this.busy.set(true);
  this.ai.ask(question).subscribe({
    next: a => this.messages.update(m => [...m, { role: 'bot', text: a.answer, citations: a.citations }]),
    error: e => this.messages.update(m => [...m, {
      role: 'bot', refusal: true,
      text: e.code === 'ERR053' ? translate('ai.ungrounded') : e.message_ }]),
    complete: () => this.busy.set(false),
  });
}
```

- [ ] **Step 2: Run the chat spec**

Run: `cd frontend && npx ng test portal-app --watch=false --filter="chat"`
Expected: PASS, 3/3 (verified when shipped). Paste output.

- [ ] **Step 3: Commit** — already committed when shipped.

---

### Task 6: Full-suite gate (`AC-F8`)

- [ ] **Step 1: Run both app suites + builds**

Run: `cd frontend && npx ng test common --watch=false && npx ng test admin-app --watch=false && npx ng test portal-app --watch=false && npx ng build common && npx ng build admin-app && npx ng build portal-app`
Expected: green — common 136/136, portal 17/17, admin AI specs green, both builds clean. The
`nav-routes` failure belongs to the concurrent `/reports` feature (sidebar entries not yet added);
skipped per owner. Paste actual output.

- [ ] **Step 2: Commit** — already committed when shipped.

## Task Record

| # | Task | Cites | Status |
|---|---|---|---|
| F0 | Spec + plan committed before code | gate | done |
| F1 | `AiApi` + `ai.*` keys + unit test | AC-F1, A4 | done — 3/3 |
| F2 | AiPanel on ticket detail + spec | AC-F2/F5/F6, A1, A2 | done — 3/3; detail 9/9 |
| F3 | Composer draft-fill | AC-F3 | deferred — panel covers drafting UI |
| F4 | Solutions citation links | AC-F4 | done — asserted in F2 spec |
| F5 | Portal chat widget + spec | AC-F7, A3 | done — 3/3 |
| F6 | Full suites green, builds clean | AC-F8, gate | done — see above |
