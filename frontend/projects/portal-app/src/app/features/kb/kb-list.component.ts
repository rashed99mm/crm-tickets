import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  ContentsApi,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  ContentSummary,
  empty,
  failed,
  KbCategoryNode,
  loaded,
  loading,
  LocaleStore,
  PagedResult,
  TranslatePipe,
} from 'common';

const FALLBACK_CATEGORIES: readonly KbCategoryNode[] = [
  { id: 'getting-started', name: 'Getting Started', parentId: null, children: [] },
  { id: 'account-management', name: 'Account Management', parentId: null, children: [] },
  { id: 'billing', name: 'Billing', parentId: null, children: [] },
  { id: 'technical-support', name: 'Technical Support', parentId: null, children: [] },
];

/** `knowledge_base_management` mockup, simplified — the customer-facing article list. */
@Component({
  selector: 'portal-kb-list',
  imports: [
    RouterLink,
    CsCard,
    CsIcon,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './kb-list.component.html',
})
export default class PortalKbListComponent {
  private readonly api = inject(ContentsApi);
  protected readonly locale = inject(LocaleStore);

  readonly state = signal<AsyncState<PagedResult<ContentSummary>>>(loading());
  readonly search = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);

  /** All active KB categories (loaded from the public endpoint). */
  readonly categories = signal<readonly KbCategoryNode[]>([]);
  /** The currently selected category id, or null when browsing all. */
  readonly categoryId = signal<string | null>(null);

  /** The full tree of root categories (no parent) for the grid. */
  readonly rootCategories = computed(() => this.categories().filter(c => !c.parentId));

  /** The currently selected category node, if any. */
  readonly currentCategory = computed<KbCategoryNode | null>(() => {
    const id = this.categoryId();
    if (!id) return null;
    return this.flatCategories().find(c => c.id === id) ?? null;
  });

  /** Flat list of all categories for lookup. */
  private readonly flatCategories = computed(() => this.flatten(this.categories()));

  /** Pagination page-size choices; kept in the component so the template carries no literals. */
  readonly pageSizeOptions = [5, 10, 20, 50];

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.totalCount() / this.pageSize())),
  );

  /** The next button reads its disabled state here — a `>=` in an attribute would parse as markup. */
  readonly isLastPage = computed(() => this.page() >= this.totalPages());

  readonly articles = computed<readonly ContentSummary[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data.items : [];
  });

  readonly listError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  /** Whether any filter (category or search) is active. */
  readonly isFiltered = computed(() => !!this.categoryId() || !!this.search().trim());

  /** Published FAQ articles, shown in a separate section above the searchable browse list (US-513). */
  readonly faq = signal<readonly ContentSummary[]>([]);
  readonly faqTotal = signal(0);
  readonly faqSkip = signal(0);
  readonly faqTake = signal(8);
  readonly expandedFaqId = signal<string | null>(null);

  readonly promotedArticles = computed(() =>
    this.articles().filter(article => !article.isFaq).slice(0, 3),
  );

  coverImage(article: ContentSummary): string {
    if (article.featuredImageUrl) return article.featuredImageUrl;
    return article.contentType === 'Guide'
      ? 'https://images.unsplash.com/photo-1456324504439-367CEE3b3c32?auto=format&fit=crop&w=1200&q=80'
      : article.isFaq
        ? 'https://images.unsplash.com/photo-1553877522-43269d4ea984?auto=format&fit=crop&w=1200&q=80'
        : 'https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=1200&q=80';
  }

  /** Current year for the footer copyright. */
  readonly currentYear = new Date().getFullYear();

  /** Exposed to the template for computing pagination summary bounds. */
  protected readonly min = Math.min;
  protected readonly max = Math.max;

  constructor() {
    this.loadCategories();
    this.load();
    this.loadFaq();
  }

  private loadCategories(): void {
    this.api.categories().subscribe({
      next: (cats) => this.categories.set(cats.length ? cats : FALLBACK_CATEGORIES),
      error: () => this.categories.set(FALLBACK_CATEGORIES),
    });
  }

  private loadFaq(): void {
    const term = this.search().trim();
    this.api.faq(term || undefined, this.faqSkip(), this.faqTake()).subscribe({
      next: (result) => {
        this.faq.set(result.items);
        this.faqTotal.set(result.totalCount);
      },
      error: () => {
        this.faq.set([]);
        this.faqTotal.set(0);
      },
    });
  }

  load(): void {
    this.state.set(loading());
    const term = this.search().trim();
    const catId = this.categoryId() ?? undefined;
    this.api.list(term || undefined, this.page(), this.pageSize(), catId).subscribe({
      next: (result) => {
        this.totalCount.set(result.totalCount);
        this.state.set(result.items.length === 0 && this.page() === 1 ? empty() : loaded(result));
      },
      error: (error: unknown) =>
        this.state.set(
          failed(error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0)),
        ),
    });
  }

  onSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  submitSearch(): void {
    this.page.set(1);
    this.faqSkip.set(0);
    this.load();
    this.loadFaq();
  }

  goToPage(next: number): void {
    if (next < 1 || next > this.totalPages() || next === this.page()) {
      return;
    }
    this.page.set(next);
    this.load();
  }

  changePageSize(event: Event): void {
    const size = Number((event.target as HTMLSelectElement).value);
    if (!Number.isFinite(size) || size <= 0) {
      return;
    }
    this.pageSize.set(size);
    this.page.set(1);
    this.load();
  }

  /** Apply a quick-suggestion tag from the hero (Reset Password / API Docs / Billing). */
  applySuggestion(term: string): void {
    this.search.set(term);
    this.submitSearch();
  }

  toggleFaq(id: string): void {
    this.expandedFaqId.update(current => current === id ? null : id);
  }

  /** Select a category from the grid or breadcrumb. */
  selectCategory(category: KbCategoryNode): void {
    this.categoryId.set(category.id);
    this.page.set(1);
    this.search.set('');
    this.load();
  }

  /** Clear the category filter and return to the full list. */
  clearCategory(): void {
    this.categoryId.set(null);
    this.page.set(1);
    this.load();
  }

  private flatten(nodes: readonly KbCategoryNode[]): KbCategoryNode[] {
    return nodes.flatMap(n => [n, ...this.flatten(n.children)]);
  }
}
