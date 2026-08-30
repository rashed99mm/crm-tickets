# FEAT-21 · AI Assist — Stitch-Faithful Right Rail (Frontend)

> **Spec:** `docs/superpowers/specs/EPIC-11-US-701-feat-21-stitch-right-rail.md` (AC-F9..AC-F15)
> **Builds on:** `docs/superpowers/specs/EPIC-13-US-311-feat-21-frontend-design.md`, `EPIC-13-US-311-feat-21-frontend/` plan
>
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure the right-rail AI panel on the admin-app ticket detail screen to match
the three Stitch mockups: a chrome-free "AI Assistant" header band, four `<cs-card>` panels in
order (Context Summary, Suggested Replies, Knowledge Base, Categories), a sentiment chip on
the summary, an Insert affordance on each suggested reply, and a "Draft with AI" trigger in
the composer toolbar.

**Architecture:** `AiPanelComponent` keeps the same envelope (`ticketId` input, `available` signal)
but renders four cards and emits an `insert(text)` `output()` to the parent. `TicketDetailComponent`
wires that to a new `insertDraft(text)` method on the existing `TicketMessagesComponent` via a
template reference variable. `TicketMessagesComponent` exposes `insertDraft(text)` (public method)
and adds a "Draft with AI" button to the composer toolbar that calls `AiApi.draftReply(ticketId)`.

**Tech stack:** Angular 20 standalone + signals, no new packages, no new routes.

---

### Task 1: Tighten the typed DTO and add i18n keys (AC-F1, AC-F15)

**Files:**
- Modify: `frontend/projects/common/src/lib/ai/ai.api.ts`
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts`

- [ ] **Step 1: Add typed payload accessors to the DTO** (no behaviour change, just typing)

```typescript
// frontend/projects/common/src/lib/ai/ai.api.ts
export interface AiSuggestionDto {
  readonly id: string;
  readonly kind: 'Summary' | 'Categories' | 'Reply' | 'Solutions';
  readonly payload: unknown;
  readonly status: 'Pending' | 'Accepted' | 'Rejected';
  readonly edited: boolean;
}

/** AC-21.11 — the summary payload shape. */
export interface AiSummaryPayload {
  readonly text: string;
  readonly sentiment: 'Frustrated' | 'Neutral' | 'Satisfied' | null;
}

/** AC-21.12 — the reply payload shape. */
export interface AiReplyPayload {
  readonly drafts: readonly string[];
}

/** AC-21.14 — the solutions payload shape. */
export interface AiSolutionsPayload {
  readonly articles: readonly { id: string; title: string }[];
}

/** AC-21.13 — the categories payload shape. */
export interface AiCategoriesPayload {
  readonly options: readonly { name: string }[];
}
```

- [ ] **Step 2: Add new i18n keys (en + ar)**

Append to `frontend/projects/common/src/lib/i18n/translations.ts` under the `ai` namespace:

```typescript
'ai.title': { en: 'AI Assistant', ar: 'مساعد الذكاء الاصطناعي' },
'ai.suggestedReplies': { en: 'Suggested Replies', ar: 'ردود مقترحة' },
'ai.knowledgeBase': { en: 'Knowledge Base', ar: 'قاعدة المعرفة' },
'ai.relatedArticles': { en: 'Related Articles', ar: 'مقالات ذات صلة' },
'ai.noDrafts': { en: 'No drafts available', ar: 'لا توجد مسودات' },
'ai.insert': { en: 'Insert', ar: 'إدراج' },
'ai.draftWithAi': { en: 'Draft with AI', ar: 'مسودة بالذكاء الاصطناعي' },
'ai.sentiment.frustrated': { en: 'Frustrated', ar: 'محبط' },
'ai.sentiment.neutral': { en: 'Neutral', ar: 'محايد' },
'ai.sentiment.satisfied': { en: 'Satisfied', ar: 'راضٍ' },
```

- [ ] **Step 3: Verify the build**

```powershell
cd frontend && npx ng build common 2>&1 | Select-String "error|Error|Complete"
```

Expected: clean.

- [ ] **Step 4: Commit**

```powershell
git add frontend/projects/common/src/lib/ai/ai.api.ts frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat(feat-21): typed ai suggestion payloads and i18n keys for right rail"
```

---

### Task 2: Restructure `AiPanelComponent` into four cards (AC-F9, AC-F10, AC-F11, AC-F12)

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ai-panel.component.ts`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ai-panel.component.html`

- [ ] **Step 1: Rewrite the component class**

```typescript
// frontend/projects/admin-app/src/app/features/tickets/ai-panel.component.ts
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  AiApi,
  AiSuggestionDto,
  CsButton,
  CsCard,
  CsErrorState,
  CsIcon,
  failed,
  loaded,
  loading,
  CsPlaceholder,
  TranslatePipe,
} from 'common';

/**
 * FEAT-21 — the Stitch-faithful right rail. One chrome-free "AI Assistant" header band, four
 * <cs-card>s in order: Context Summary, Suggested Replies, Knowledge Base, Categories.
 *
 * A1: the first ERR052 answer flips `available` off for good and the whole rail disappears,
 * instead of leaving buttons that can only ever fail.
 * A2: every AI draft is read-only here with explicit Accept/Reject; nothing posts until Accept.
 *
 * The Insert buttons on Suggested Replies call `insert.emit(draft)` — the parent (ticket-detail)
 * wires that to <admin-ticket-messages>.insertDraft(). No shared service: the surfaces are
 * unrelated beyond this one handshake.
 */
@Component({
  selector: 'admin-ai-panel',
  imports: [RouterLink, CsCard, CsIcon, CsButton, CsErrorState, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ai-panel.component.html',
})
export class AiPanelComponent {
  private readonly api = inject(AiApi);

  readonly ticketId = input.required<string>();

  /** A1 — flipped off permanently on the first not-configured answer. */
  readonly available = signal(true);

  // Per-card state. Each card flips between idle / loading / error / loaded; only one
  // suggestion is ever shown at a time per card so a new call replaces the previous result.
  readonly summary = signal<AsyncState<AiSuggestionDto>>(loading());
  readonly replies = signal<AsyncState<AiSuggestionDto>>(loading());
  readonly solutions = signal<AsyncState<AiSuggestionDto>>(loading());
  readonly categories = signal<AsyncState<AiSuggestionDto>>(loading());

  readonly summaryText = computed(() => this.summaryValue()?.text ?? '');
  readonly summarySentiment = computed(() => this.summaryValue()?.sentiment ?? null);
  readonly drafts = computed(() => this.replyValue()?.drafts ?? []);
  readonly articleLinks = computed(() => this.solutionsValue()?.articles ?? []);
  readonly categoryNames = computed(() => this.categoryNamesValue() ?? []);

  /** Emits a draft string the user chose to insert; the parent writes it into the composer. */
  readonly insert = output<string>();

  summarise(): void {
    this.run('summary', () => this.api.summarise(this.ticketId()), this.summary);
  }

  suggestReplies(): void {
    this.run('replies', () => this.api.draftReply(this.ticketId()), this.replies);
  }

  suggestSolutions(): void {
    this.run('solutions', () => this.api.suggestSolutions(this.ticketId()), this.solutions);
  }

  suggestCategories(): void {
    this.run('categories', () => this.api.suggestCategories(this.ticketId()), this.categories);
  }

  resolve(kind: 'summary' | 'replies' | 'solutions' | 'categories', action: 'accept' | 'reject'): void {
    const state = this.stateFor(kind);
    if (state().status !== 'loaded') return;
    const current = state() as { status: 'loaded'; data: AiSuggestionDto };
    this.api.resolve(this.ticketId(), current.data.id, action).subscribe({
      next: () => this.stateFor(kind).set(loading()),
      error: (e: unknown) => this.stateFor(kind).set(this.toFailure(e)),
    });
  }

  onInsert(draft: string): void {
    this.insert.emit(draft);
  }

  // --- payload accessors with defensive shape fallbacks -----------------------------------------

  private summaryValue(): { text: string; sentiment: 'Frustrated' | 'Neutral' | 'Satisfied' | null } | null {
    const state = this.summary();
    if (state.status !== 'loaded') return null;
    const payload = state.data.payload as { text?: unknown; sentiment?: unknown } | undefined;
    const text = typeof payload?.text === 'string' ? payload.text : '';
    const sentiment = payload?.sentiment;
    const ok = sentiment === 'Frustrated' || sentiment === 'Neutral' || sentiment === 'Satisfied';
    return { text, sentiment: ok ? sentiment : null };
  }

  private replyValue(): { drafts: readonly string[] } | null {
    const state = this.replies();
    if (state.status !== 'loaded') return null;
    const payload = state.data.payload as { drafts?: unknown } | undefined;
    const drafts = Array.isArray(payload?.drafts)
      ? payload.drafts.filter((d): d is string => typeof d === 'string' && d.length > 0)
      : [];
    return { drafts };
  }

  private solutionsValue(): { articles: readonly { id: string; title: string }[] } | null {
    const state = this.solutions();
    if (state.status !== 'loaded') return null;
    const payload = state.data.payload as { articles?: unknown } | undefined;
    const articles = Array.isArray(payload?.articles)
      ? payload.articles
          .filter((a): a is { id: string; title: string } =>
            typeof a === 'object' && a !== null && typeof (a as { id?: unknown }).id === 'string' &&
            typeof (a as { title?: unknown }).title === 'string')
      : [];
    return { articles };
  }

  private categoryNamesValue(): readonly string[] | null {
    const state = this.categories();
    if (state.status !== 'loaded') return null;
    const payload = state.data.payload as { options?: unknown } | undefined;
    return Array.isArray(payload?.options)
      ? payload.options
          .map((o) => (typeof o === 'object' && o !== null && typeof (o as { name?: unknown }).name === 'string'
            ? (o as { name: string }).name : null))
          .filter((n): n is string => n !== null)
      : [];
  }

  // --- request plumbing -------------------------------------------------------------------------

  private run(
    kind: 'summary' | 'replies' | 'solutions' | 'categories',
    call: () => ReturnType<AiApi['summarise']>,
    target: ReturnType<typeof signal<AsyncState<AiSuggestionDto>>>,
  ): void {
    if (!this.available()) return;
    target.set(loading());
    call().subscribe({
      next: (suggestion) => target.set(loaded(suggestion)),
      error: (e: unknown) => {
        if (e instanceof ApiError && e.code === 'ERR052') {
          this.available.set(false);
          return;
        }
        target.set(this.toFailure(e));
      },
    });
  }

  private stateFor(kind: 'summary' | 'replies' | 'solutions' | 'categories') {
    return kind === 'summary' ? this.summary :
           kind === 'replies' ? this.replies :
           kind === 'solutions' ? this.solutions :
           this.categories;
  }

  private toFailure(e: unknown): { status: 'error'; error: ApiError } {
    return failed(e instanceof ApiError
      ? e
      : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0));
  }
}
```

- [ ] **Step 2: Rewrite the template**

```html
<!-- frontend/projects/admin-app/src/app/features/tickets/ai-panel.component.html -->
@if (available()) {
  <div class="flex flex-col gap-4" data-testid="ai-rail">
    <!-- AC-F11 — chrome-free header band, one per page, no <cs-card> chrome. -->
    <div class="flex items-center gap-2">
      <cs-icon name="auto_awesome" [size]="20" />
      <h2 class="font-headline-md text-headline-md font-semibold text-on-surface">
        {{ 'ai.title' | t }}
      </h2>
    </div>

    <!-- AC-F12 — Context Summary first. -->
    <cs-card [heading]="'ai.summary' | t" data-testid="ai-summary-card">
      <div class="p-4">
        <cs-button variant="secondary" (pressed)="summarise()" [disabled]="summary().status === 'loading'">
          <cs-icon name="summarize" [size]="16" />
          {{ 'ai.summary' | t }}
        </cs-button>

        @switch (summary().status) {
          @case ('error') {
            @if (summary(); as s) { @if (s.status === 'error') {
              <cs-error-state [error]="s.error" (retry)="summarise()" />
            } }
          }
          @case ('loaded') {
            @if (summaryText(); as text) {
              <div class="mt-3 flex flex-col gap-2" data-testid="ai-summary-result">
                <!-- AC-F9 — sentiment chip. -->
                @if (summarySentiment(); as sentiment) {
                  <span
                    class="inline-flex w-fit items-center gap-1 rounded px-2 py-0.5 font-label-md text-label-md"
                    [class.bg-error-container]="sentiment === 'Frustrated'"
                    [class.text-on-error-container]="sentiment === 'Frustrated'"
                    [class.bg-surface-container-highest]="sentiment === 'Neutral'"
                    [class.text-on-surface]="sentiment === 'Neutral'"
                    [class.bg-tertiary-container]="sentiment === 'Satisfied'"
                    [class.text-on-tertiary-container]="sentiment === 'Satisfied'"
                    data-testid="ai-summary-sentiment"
                  >
                    <cs-icon
                      [name]="sentiment === 'Frustrated' ? 'sentiment_dissatisfied' :
                              sentiment === 'Neutral'    ? 'sentiment_neutral' :
                                                          'sentiment_satisfied'"
                      [size]="14"
                    />
                    {{ 'ai.sentiment.' + (sentiment | lowercase) | t }}
                  </span>
                }
                <p class="whitespace-pre-line text-body-md text-on-surface">{{ text }}</p>
              </div>
            }
            <!-- AC-F6 — Accept/Reject only while Pending. -->
            <div class="mt-3 flex items-center gap-2">
              <cs-button (pressed)="resolve('summary', 'accept')">
                {{ 'ai.accept' | t }}
              </cs-button>
              <cs-button variant="ghost" (pressed)="resolve('summary', 'reject')">
                {{ 'ai.reject' | t }}
              </cs-button>
            </div>
          }
        }
      </div>
    </cs-card>

    <!-- AC-F12 — Suggested Replies second. -->
    <cs-card [heading]="'ai.suggestedReplies' | t" data-testid="ai-replies-card">
      <div class="p-4">
        <cs-button variant="secondary" (pressed)="suggestReplies()" [disabled]="replies().status === 'loading'">
          <cs-icon name="reply" [size]="16" />
          {{ 'ai.draftWithAi' | t }}
        </cs-button>

        @switch (replies().status) {
          @case ('error') {
            @if (replies(); as s) { @if (s.status === 'error') {
              <cs-error-state [error]="s.error" (retry)="suggestReplies()" />
            } }
          }
          @case ('loaded') {
            @if (drafts().length > 0) {
              <ul class="mt-3 flex flex-col gap-2" data-testid="ai-replies-list">
                @for (draft of drafts(); track $index) {
                  <li>
                    <button
                      type="button"
                      class="group flex w-full flex-col items-start gap-1 rounded-lg border border-outline-variant p-2 text-start text-body-sm text-on-surface hover:bg-surface-container-high"
                      (click)="onInsert(draft)"
                      [attr.data-testid]="'ai-insert-' + $index"
                    >
                      <span class="line-clamp-2">{{ draft }}</span>
                      <span
                        class="text-label-md text-primary opacity-0 group-hover:opacity-100"
                        data-testid="ai-insert-label"
                      >
                        {{ 'ai.insert' | t }}
                      </span>
                    </button>
                  </li>
                }
              </ul>
            } @else {
              <p class="mt-3 text-body-sm text-on-surface-variant" data-testid="ai-no-drafts">
                {{ 'ai.noDrafts' | t }}
              </p>
            }
            <div class="mt-3 flex items-center gap-2">
              <cs-button (pressed)="resolve('replies', 'accept')">{{ 'ai.accept' | t }}</cs-button>
              <cs-button variant="ghost" (pressed)="resolve('replies', 'reject')">{{ 'ai.reject' | t }}</cs-button>
            </div>
          }
        }
      </div>
    </cs-card>

    <!-- AC-F12 — Knowledge Base third. -->
    <cs-card [heading]="'ai.knowledgeBase' | t" data-testid="ai-solutions-card">
      <div class="p-4">
        <cs-button variant="secondary" (pressed)="suggestSolutions()" [disabled]="solutions().status === 'loading'">
          <cs-icon name="menu_book" [size]="16" />
          {{ 'ai.relatedArticles' | t }}
        </cs-button>

        @switch (solutions().status) {
          @case ('error') {
            @if (solutions(); as s) { @if (s.status === 'error') {
              <cs-error-state [error]="s.error" (retry)="suggestSolutions()" />
            } }
          }
          @case ('loaded') {
            @if (articleLinks().length > 0) {
              <ul class="mt-3 flex flex-col gap-2" data-testid="ai-solutions-list">
                @for (article of articleLinks(); track article.id) {
                  <li>
                    <a
                      [routerLink]="['/knowledge-base', article.id]"
                      class="flex items-start gap-2 text-body-sm font-semibold text-primary hover:underline"
                    >
                      <cs-icon name="article" [size]="16" />
                      {{ article.title }}
                    </a>
                  </li>
                }
              </ul>
            } @else {
              <p class="mt-3 text-body-sm text-on-surface-variant">{{ 'ai.noDrafts' | t }}</p>
            }
            <div class="mt-3 flex items-center gap-2">
              <cs-button (pressed)="resolve('solutions', 'accept')">{{ 'ai.accept' | t }}</cs-button>
              <cs-button variant="ghost" (pressed)="resolve('solutions', 'reject')">{{ 'ai.reject' | t }}</cs-button>
            </div>
          }
        }
      </div>
    </cs-card>

    <!-- AC-F12 — Categories fourth. -->
    <cs-card [heading]="'ai.categories' | t" data-testid="ai-categories-card">
      <div class="p-4">
        <cs-button variant="secondary" (pressed)="suggestCategories()" [disabled]="categories().status === 'loading'">
          <cs-icon name="category" [size]="16" />
          {{ 'ai.categories' | t }}
        </cs-button>

        @switch (categories().status) {
          @case ('error') {
            @if (categories(); as s) { @if (s.status === 'error') {
              <cs-error-state [error]="s.error" (retry)="suggestCategories()" />
            } }
          }
          @case ('loaded') {
            @if (categoryNames().length > 0) {
              <ul class="mt-3 list-disc ps-5 text-body-md text-on-surface" data-testid="ai-categories-list">
                @for (name of categoryNames(); track name) {
                  <li>{{ name }}</li>
                }
              </ul>
            } @else {
              <p class="mt-3 text-body-sm text-on-surface-variant">{{ 'ai.noDrafts' | t }}</p>
            }
            <div class="mt-3 flex items-center gap-2">
              <cs-button (pressed)="resolve('categories', 'accept')">{{ 'ai.accept' | t }}</cs-button>
              <cs-button variant="ghost" (pressed)="resolve('categories', 'reject')">{{ 'ai.reject' | t }}</cs-button>
            </div>
          }
        }
      </div>
    </cs-card>
  </div>
}
```

- [ ] **Step 3: Build**

```powershell
cd frontend && npx ng build admin-app 2>&1 | Select-String "error|Error|Complete" | Select-Object -First 20
```

Expected: clean build, no missing imports.

- [ ] **Step 4: Commit**

```powershell
git add frontend/projects/admin-app/src/app/features/tickets/ai-panel.component.ts frontend/projects/admin-app/src/app/features/tickets/ai-panel.component.html
git commit -m "feat(feat-21): stitch-faithful right rail with four ai cards"
```

---

### Task 3: Add Draft-with-AI to `TicketMessagesComponent` and an `insertDraft()` public method (AC-F13, AC-F14)

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.ts`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.html`

- [ ] **Step 1: Add the `AiApi` injection, a `drafting` signal, and a public `insertDraft()` method**

```typescript
// frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.ts
import {
  // ...existing imports...
  AiApi,
} from 'common';

// inside the class:
  private readonly ai = inject(AiApi);

  readonly drafting = signal(false);
  readonly aiAvailable = signal(true);

  /** AC-F14 — the right-rail card calls this via a parent/child template ref. */
  insertDraft(text: string): void {
    this.body.set(text);
    this.submitError.set(null);
  }

  draftWithAi(): void {
    if (this.drafting() || !this.aiAvailable()) return;
    this.drafting.set(true);
    this.ai.draftReply(this.ticketId()).subscribe({
      next: (suggestion) => {
        // The reply payload is { drafts: string[] } — AC-21.12.
        const payload = suggestion.payload as { drafts?: unknown } | undefined;
        const first = Array.isArray(payload?.drafts)
          ? payload.drafts.find((d): d is string => typeof d === 'string' && d.length > 0)
          : undefined;
        if (first) this.insertDraft(first);
        this.drafting.set(false);
      },
      error: (e: unknown) => {
        // AI-37 / A5 — a transient provider error does not flip the affordance off; only
        // the deployment-level ERR052 does. The toolbar button stays visible.
        if (e instanceof ApiError && e.code === 'ERR052') {
          this.aiAvailable.set(false);
        }
        this.drafting.set(false);
      },
    });
  }
```

- [ ] **Step 2: Add the Draft with AI button to the composer toolbar in the template**

```html
<!-- inside the form-actions area of ticket-messages.component.html, alongside the Submit button -->
<div class="flex items-center gap-2">
  @if (aiAvailable()) {
    <cs-button
      type="button"
      variant="secondary"
      [busy]="drafting()"
      [disabled]="!aiAvailable() || drafting() || saving()"
      (pressed)="draftWithAi()"
      data-testid="composer-draft-with-ai"
    >
      <cs-icon name="auto_awesome" [size]="16" />
      {{ 'ai.draftWithAi' | t }}
    </cs-button>
  }
  <cs-button type="button" [busy]="saving()" [disabled]="!canSubmit()" (pressed)="log()">
    {{ 'messages.submit' | t }}
  </cs-button>
</div>
```

- [ ] **Step 3: Wire `AiPanelComponent.insert` to `TicketMessagesComponent.insertDraft` in `TicketDetailComponent`**

```html
<!-- frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html -->
<!-- replace the existing <admin-ai-panel [ticketId]="id()" /> with: -->
<admin-ai-panel
  #aiPanel
  [ticketId]="id()"
  (insert)="messages?.insertDraft($event)"
/>

<!-- and ensure `messages` is declared as a template ref on the messages component: -->
<admin-ticket-messages #messages [ticketId]="t.id" />
```

(No change is needed in `TicketDetailComponent.ts` for this — template references work via the
component's view children without explicit TypeScript. The `(insert)` handler runs after view
init so the `messages` template ref is always available when the user clicks Insert.)

- [ ] **Step 4: Build**

```powershell
cd frontend && npx ng build admin-app 2>&1 | Select-String "error|Error|Complete" | Select-Object -First 20
```

Expected: clean.

- [ ] **Step 5: Commit**

```powershell
git add frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.ts frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.html frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html
git commit -m "feat(feat-21): draft with ai in composer and insertdraft handshake"
```

---

### Task 4: Extend component tests (AC-F2, AC-F3, AC-F4, AC-F5, AC-F6, AC-F9, AC-F10, AC-F13, AC-F14)

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ai-panel.component.spec.ts`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.spec.ts`

- [ ] **Step 1: Update `ai-panel.component.spec.ts`**

Add tests for: the four cards (each data-testid), the sentiment chip rendering, the Insert buttons
emitting the right draft, the Categories and Solutions card content, the ERR052 first-answer rule,
the Accept/Reject lifecycle. Existing tests are mostly reusable — the new structure uses
`data-testid="ai-summary-card"` etc. Update the existing assertion that reads
`data-testid="ai-panel"` (now `ai-rail` for the wrapper, `ai-summary-card` etc. for the inner
cards).

```typescript
// existing test "shows the draft with Accept/Reject while pending" — change to:
//   expect(el.querySelector('[data-testid="ai-summary-card"]')).not.toBeNull();
//   expect(el.querySelector('[data-testid="ai-summary-result"]')).not.toBeNull();
```

Add the new cases:

```typescript
it('renders the sentiment chip for Frustrated', () => {
  const fixture = create();
  fixture.componentInstance.summarise();
  http.expectOne('/api/Tickets/t1/ai/summary').flush({
    id: 's1', kind: 'Summary',
    payload: { text: 'Customer is locked out.', sentiment: 'Frustrated' },
    status: 'Pending', edited: false,
  });
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  expect(el.querySelector('[data-testid="ai-summary-sentiment"]')?.textContent).toContain('Frustrated');
});

it('omits the chip when sentiment is null', () => {
  const fixture = create();
  fixture.componentInstance.summarise();
  http.expectOne('/api/Tickets/t1/ai/summary').flush({
    id: 's1', kind: 'Summary',
    payload: { text: 'Customer is locked out.', sentiment: null },
    status: 'Pending', edited: false,
  });
  fixture.detectChanges();
  expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="ai-summary-sentiment"]')).toBeNull();
});

it('lists drafts and emits insert on click', () => {
  const fixture = create();
  fixture.componentInstance.suggestReplies();
  http.expectOne('/api/Tickets/t1/ai/reply').flush({
    id: 'r1', kind: 'Reply',
    payload: { drafts: ['First.', 'Second.', 'Third.'] },
    status: 'Pending', edited: false,
  });
  fixture.detectChanges();

  const inserts: string[] = [];
  fixture.componentInstance.insert.subscribe((v: string) => inserts.push(v));

  const buttons = (fixture.nativeElement as HTMLElement)
    .querySelectorAll<HTMLButtonElement>('[data-testid="ai-insert-0"]');
  expect(buttons.length).toBe(1);
  buttons[0].click();
  expect(inserts).toEqual(['First.']);
});

it('renders knowledge base links to the article route', () => {
  const fixture = create();
  fixture.componentInstance.suggestSolutions();
  http.expectOne('/api/Tickets/t1/ai/solutions').flush({
    id: 'k1', kind: 'Solutions',
    payload: { articles: [{ id: 'a1', title: 'Reset password' }] },
    status: 'Pending', edited: false,
  });
  fixture.detectChanges();
  const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>(
    '[data-testid="ai-solutions-list"] a',
  );
  expect(link?.getAttribute('href')).toContain('/knowledge-base/a1');
  expect(link?.textContent).toContain('Reset password');
});

it('renders the categories list', () => {
  const fixture = create();
  fixture.componentInstance.suggestCategories();
  http.expectOne('/api/Tickets/t1/ai/categories').flush({
    id: 'c1', kind: 'Categories',
    payload: { options: [{ name: 'Billing' }, { name: 'Access' }] },
    status: 'Pending', edited: false,
  });
  fixture.detectChanges();
  const items = (fixture.nativeElement as HTMLElement)
    .querySelectorAll<HTMLLIElement>('[data-testid="ai-categories-list"] li');
  expect(items.length).toBe(2);
  expect(items[0].textContent).toContain('Billing');
});
```

Update the existing ERR052 test to assert the new structure:

```typescript
it('hides the whole rail after the first ERR052', () => {
  // unchanged: http.expectOne(...).flush({ 503 ERR052 envelope });
  expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="ai-rail"]')).toBeNull();
});
```

- [ ] **Step 2: Update `ticket-messages.component.spec.ts`**

Add a Draft with AI test and an insertDraft test:

```typescript
it('Draft with AI fills the composer body with the first draft', () => {
  // extend the useValue with draftReply: vi.fn(() => of({ id: 'r1', kind: 'Reply', payload: { drafts: ['A.', 'B.'] }, status: 'Pending', edited: false }))
  // extend the providers with { provide: AiApi, useValue: { draftReply } }
  // (other ai methods can be vi.fn())
  // ... trigger the button via (click)
});

it('insertDraft writes the body and clears any field error', () => {
  // set submitError via a prior failed log(), then call insertDraft('hello')
  // assert the textarea content and submitError nullity
});
```

- [ ] **Step 3: Run the admin-app test suite**

```powershell
cd frontend && npx ng test admin-app --watch=false 2>&1 | Select-String "Tests|FAIL|PASS|Error" | Select-Object -First 30
```

Expected: all pass.

- [ ] **Step 4: Build admin-app**

```powershell
cd frontend && npx ng build admin-app 2>&1 | Select-String "error|Error|Complete" | Select-Object -First 20
```

Expected: clean.

- [ ] **Step 5: Commit**

```powershell
git add frontend/projects/admin-app/src/app/features/tickets/ai-panel.component.spec.ts frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.spec.ts
git commit -m "test(feat-21): right-rail cards sentiment insert draft-with-ai"
```

---

## Ship gate

- All four tasks committed.
- `npx ng test admin-app --watch=false` green with output pasted.
- `npx ng build admin-app` clean.
- Story statuses flipped in `US-704` and `US-706` (already done).
- Delivery plan row for `FEAT-21` updated to **shipped**.
- Rubric traceability row updated.
