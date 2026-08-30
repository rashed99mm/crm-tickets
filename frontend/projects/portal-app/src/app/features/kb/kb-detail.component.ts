import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  ContentsApi,
  CsCard,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  ContentSummary,
  failed,
  loaded,
  loading,
  KbAiSidebarComponent,
  LocaleStore,
  SessionStore,
  TranslatePipe,
} from 'common';

@Component({
  selector: 'portal-kb-detail',
  imports: [RouterLink, CsCard, CsIcon, CsLoadingState, CsErrorState, TranslatePipe, KbAiSidebarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './kb-detail.component.html',
})
export default class PortalKbDetailComponent {
  private readonly api = inject(ContentsApi);
  protected readonly locale = inject(LocaleStore);
  protected readonly session = inject(SessionStore);

  readonly id = input.required<string>();
  readonly state = signal<AsyncState<ContentSummary>>(loading());

  readonly data = computed<ContentSummary | null>(() => {
    const s = this.state();
    return s.status === 'loaded' ? s.data : null;
  });

  readonly error = computed<ApiError | null>(() => {
    const s = this.state();
    return s.status === 'error' ? s.error : null;
  });

  /** Set once the visitor has cast a helpfulness vote (US-508). */
  readonly voted = signal<boolean | null>(null);
  readonly voting = signal(false);
  readonly voteError = signal<string | null>(null);

  readonly isAuthenticated = this.session.isAuthenticated;

  constructor() {
    effect(() => {
      const articleId = this.id();
      this.voted.set(null);
      this.voteError.set(null);
      this.loadArticle(articleId);
    });
  }

  private loadArticle(articleId: string): void {
    this.state.set(loading());
    this.api.get(articleId).subscribe({
      next: (article) => this.state.set(loaded(article)),
      error: (error: unknown) =>
        this.state.set(
          failed(error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0)),
        ),
    });
  }

  retry(): void {
    this.loadArticle(this.id());
  }

  tagLabel(tag: string): string {
    return `#${tag}`;
  }

  coverImage(article: ContentSummary): string {
    if (article.featuredImageUrl) return article.featuredImageUrl;
    return article.contentType === 'Guide'
      ? 'https://images.unsplash.com/photo-1456324504439-367CEE3b3c32?auto=format&fit=crop&w=1600&q=85'
      : article.isFaq
        ? 'https://images.unsplash.com/photo-1553877522-43269d4ea984?auto=format&fit=crop&w=1600&q=85'
        : 'https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=1600&q=85';
  }

  readingTime(article: ContentSummary): number {
    return Math.max(1, Math.ceil(article.body.trim().split(/\s+/).length / 200));
  }

  articleCategory(): string {
    const article = this.data();
    return article?.categoryName || article?.category || 'Knowledge Base';
  }

  updatedLabel(article: ContentSummary): string {
    return new Intl.DateTimeFormat(this.locale.locale(), { dateStyle: 'medium' }).format(
      new Date(article.publishedAt ?? new Date().toISOString()),
    );
  }

  vote(isHelpful: boolean): void {
    if (this.voting() || this.voted() !== null) {
      return;
    }
    this.voting.set(true);
    this.voteError.set(null);
    this.api.vote(this.id(), isHelpful).subscribe({
      next: () => {
        this.voted.set(isHelpful);
        this.voting.set(false);
      },
      error: (error: unknown) => {
        this.voting.set(false);
        this.voteError.set(error instanceof ApiError ? error.message_ : 'Something went wrong');
      },
    });
  }
}
