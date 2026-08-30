import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  ContentSummary,
  ContentsApi,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  PagedResult,
  TranslatePipe,
  empty,
  failed,
  loaded,
  loading,
} from 'common';

/** Dedicated portal collection surface for FAQ and solution articles. */
@Component({
  selector: 'portal-content-page',
  imports: [RouterLink, CsIcon, CsLoadingState, CsEmptyState, CsErrorState, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './content-page.component.html',
})
export default class PortalContentPageComponent {
  private readonly api = inject(ContentsApi);
  private readonly route = inject(ActivatedRoute);

  readonly page = signal(1);
  readonly pageSize = signal(9);
  readonly search = signal('');
  readonly state = signal<AsyncState<PagedResult<ContentSummary>>>(loading());
  readonly totalCount = signal(0);
  readonly expandedId = signal<string | null>(null);
  readonly isFaq = this.route.snapshot.url[0]?.path === 'faq';
  readonly titleKey = computed(() => this.isFaq ? 'portal.content.faqTitle' : 'portal.content.articlesTitle');
  readonly subtitleKey = computed(() => this.isFaq ? 'portal.content.faqSubtitle' : 'portal.content.articlesSubtitle');
  readonly items = computed(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data.items : [];
  });
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));
  readonly listError = computed(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  coverImage(item: ContentSummary): string {
    if (item.featuredImageUrl) return item.featuredImageUrl;
    return item.contentType === 'Guide'
      ? 'https://images.unsplash.com/photo-1456324504439-367CEE3b3c32?auto=format&fit=crop&w=1200&q=80'
      : 'https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=1200&q=80';
  }

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set(loading());
    const term = this.search().trim() || undefined;
    const request = this.isFaq
      ? this.api.faq(term, (this.page() - 1) * this.pageSize(), this.pageSize())
      : this.api.list(term, this.page(), this.pageSize());
    request.subscribe({
      next: (result) => {
        this.totalCount.set(result.totalCount);
        this.state.set(result.items.length ? loaded(result) : empty());
      },
      error: (error: unknown) => this.state.set(failed(
        error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0),
      )),
    });
  }

  submitSearch(): void {
    this.page.set(1);
    this.load();
  }

  onSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  toggleExpanded(id: string): void {
    this.expandedId.update(current => current === id ? null : id);
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.page()) return;
    this.page.set(page);
    this.load();
  }
}
