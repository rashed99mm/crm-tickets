import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  AiApi,
  AiSuggestionDto,
  CsButton,
  CsErrorState,
  CsIcon,
  failed,
  idle,
  loaded,
  loading,
  TranslatePipe,
} from 'common';

/**
 * FEAT-21 — the Stitch-faithful right rail on the ticket detail screen.
 *
 * One chrome-free "AI Assistant" header band, four <cs-card>s in the order the three mockups
 * show: Context Summary, Suggested Replies, Knowledge Base, Categories.
 *
 * A1: the first ERR052 answer flips `available` off for good and the whole rail disappears,
 *     instead of leaving buttons that can only ever fail.
 * A2: every AI draft is read-only here with explicit Accept/Reject; nothing posts until Accept.
 *
 * The Insert buttons on Suggested Replies call `insert.emit(draft)` — the parent wires that
 * to <admin-ticket-messages>.insertDraft() via a template reference. No shared service: the
 * surfaces are unrelated beyond this one handshake (AC-F14).
 */
@Component({
  selector: 'admin-ai-panel',
  imports: [RouterLink, CsIcon, CsButton, CsErrorState, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ai-panel.component.html',
})
export class AiPanelComponent {
  private readonly api = inject(AiApi);

  readonly ticketId = input.required<string>();

  /** A1 — flipped off permanently on the first not-configured answer. */
  readonly available = signal(true);

  // Per-card state. Each card flips between idle / loading / error / loaded; a new call
  // replaces the previous result, so the "Pending" review state is the loaded state.
  readonly summary = signal<AsyncState<AiSuggestionDto>>(idle());
  readonly replies = signal<AsyncState<AiSuggestionDto>>(idle());
  readonly solutions = signal<AsyncState<AiSuggestionDto>>(idle());
  readonly categories = signal<AsyncState<AiSuggestionDto>>(idle());

  readonly summaryText = computed(() => this.summaryValue()?.text ?? '');
  readonly summarySentiment = computed(
    () => this.summaryValue()?.sentiment ?? null,
  );
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
    this.run(
      'solutions',
      () => this.api.suggestSolutions(this.ticketId()),
      this.solutions,
    );
  }

  suggestCategories(): void {
    this.run(
      'categories',
      () => this.api.suggestCategories(this.ticketId()),
      this.categories,
    );
  }

  resolve(
    kind: 'summary' | 'replies' | 'solutions' | 'categories',
    action: 'accept' | 'reject',
  ): void {
    const state = this.stateFor(kind);
    if (state().status !== 'loaded') {
      return;
    }
    const current = state() as { status: 'loaded'; data: AiSuggestionDto };
    this.api.resolve(this.ticketId(), current.data.id, action).subscribe({
      next: () => this.stateFor(kind).set(loading()),
      error: (e: unknown) => this.stateFor(kind).set(this.toFailure(e)),
    });
  }

  onInsert(draft: string): void {
    this.insert.emit(draft);
  }

  // --- payload accessors with defensive shape fallbacks ---------------------------------------

  private summaryValue():
    | { text: string; sentiment: 'Frustrated' | 'Neutral' | 'Satisfied' | null }
    | null {
    const state = this.summary();
    if (state.status !== 'loaded') {
      return null;
    }
    const payload = state.data.payload as
      | { text?: unknown; sentiment?: unknown }
      | undefined;
    const text = typeof payload?.text === 'string' ? payload.text : '';
    const sentiment = payload?.sentiment;
    const ok =
      sentiment === 'Frustrated' ||
      sentiment === 'Neutral' ||
      sentiment === 'Satisfied';
    return { text, sentiment: ok ? sentiment : null };
  }

  private replyValue(): { drafts: readonly string[] } | null {
    const state = this.replies();
    if (state.status !== 'loaded') {
      return null;
    }
    const payload = state.data.payload as { drafts?: unknown } | undefined;
    const drafts = Array.isArray(payload?.drafts)
      ? payload.drafts.filter(
          (d): d is string => typeof d === 'string' && d.length > 0,
        )
      : [];
    return { drafts };
  }

  private solutionsValue():
    | { articles: readonly { id: string; title: string }[] }
    | null {
    const state = this.solutions();
    if (state.status !== 'loaded') {
      return null;
    }
    const payload = state.data.payload as { articles?: unknown } | undefined;
    const articles = Array.isArray(payload?.articles)
      ? payload.articles.filter(
          (a): a is { id: string; title: string } =>
            typeof a === 'object' &&
            a !== null &&
            typeof (a as { id?: unknown }).id === 'string' &&
            typeof (a as { title?: unknown }).title === 'string',
        )
      : [];
    return { articles };
  }

  private categoryNamesValue(): readonly string[] | null {
    const state = this.categories();
    if (state.status !== 'loaded') {
      return null;
    }
    const payload = state.data.payload as { options?: unknown } | undefined;
    return Array.isArray(payload?.options)
      ? payload.options
          .map((o) =>
            typeof o === 'object' &&
            o !== null &&
            typeof (o as { name?: unknown }).name === 'string'
              ? (o as { name: string }).name
              : null,
          )
          .filter((n): n is string => n !== null)
      : [];
  }

  // --- request plumbing ------------------------------------------------------------------------

  private run(
    kind: 'summary' | 'replies' | 'solutions' | 'categories',
    call: () => ReturnType<AiApi['summarise']>,
    target: ReturnType<typeof signal<AsyncState<AiSuggestionDto>>>,
  ): void {
    if (!this.available()) {
      return;
    }
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

  private stateFor(
    kind: 'summary' | 'replies' | 'solutions' | 'categories',
  ): ReturnType<typeof signal<AsyncState<AiSuggestionDto>>> {
    return kind === 'summary'
      ? this.summary
      : kind === 'replies'
        ? this.replies
        : kind === 'solutions'
          ? this.solutions
          : this.categories;
  }

  private toFailure(e: unknown): AsyncState<AiSuggestionDto> {
    return failed(
      e instanceof ApiError
        ? e
        : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0),
    );
  }
}
