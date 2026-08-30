import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Observable, of, switchMap } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  CategoryNode,
  ConfirmationService,
  ContentSummary,
  ContentVersion,
  CsCard,
  CsDataToolbar,
  CsDialog,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  empty,
  failed,
  KbAdminApi,
  LocaleStore,
  loaded,
  loading,
  PagedResult,
  ToastService,
  TranslatePipe,
  TranslationKey,
  DataToolbarOption,
} from 'common';

const STATUSES = ['Draft', 'Published', 'Archived'] as const;
const CONTENT_TABS = ['all', 'faqs', 'articles', 'guides'] as const;
type ContentTab = (typeof CONTENT_TABS)[number];
type AuthoringType = 'Article' | 'Guide' | 'Faq';

/** `TranslatePipe` is typed against the dictionary's key union, so template-side string
 * concatenation does not type-check — the same lookup the report screens use. */
const STATUS_LABEL_KEYS: Readonly<Record<string, TranslationKey>> = {
  Draft: 'kb.status.draft',
  Published: 'kb.status.published',
  Archived: 'kb.status.archived',
};

const TAB_LABEL_KEYS: Readonly<Record<ContentTab, TranslationKey>> = {
  all: 'kb.tabs.all',
  faqs: 'kb.tabs.faqs',
  articles: 'kb.tabs.articles',
  guides: 'kb.tabs.guides',
};

/** FEAT-18 US-509..512 — the staff authoring surface over the internal `/api/Contents`. */
@Component({
  selector: 'admin-kb-admin',
  imports: [
    FormsModule,
    RouterLink,
    CsCard,
    CsDataToolbar,
    CsDialog,
    CsIcon,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './kb-admin.component.html',
})
export default class KbAdminComponent {
  private readonly api = inject(KbAdminApi);
  private readonly confirmations = inject(ConfirmationService);
  private readonly toasts = inject(ToastService);
  private readonly locale = inject(LocaleStore);

  readonly statuses = STATUSES;
  readonly contentTabs = CONTENT_TABS;
  readonly state = signal<AsyncState<PagedResult<ContentSummary>>>(loading());
  readonly statusFilter = signal<string>('');
  readonly searchTerm = signal<string>('');
  readonly activeTab = signal<ContentTab>('all');
  readonly sortMode = signal('title');

  /** US-510/511 — the create/edit form. `editing` is the article being edited, null = create. */
  readonly formOpen = signal(false);
  readonly editing = signal<ContentSummary | null>(null);
  readonly formTitle = signal('');
  readonly formSummary = signal('');
  readonly formBody = signal('');
  readonly formTags = signal('');
  readonly authoringType = signal<AuthoringType>('Article');
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);

  /** US-512 — publish/archive mutations. */
  readonly mutating = signal(false);
  readonly mutationError = signal<string | null>(null);

  /** US-511 AC3 — version history of the article being edited, newest first (server order). */
  readonly versions = signal<readonly ContentVersion[]>([]);
  readonly categories = signal<readonly CategoryNode[]>([]);
  readonly selectedCategoryId = signal<string>('');

  readonly articles = computed<readonly ContentSummary[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data.items : [];
  });

  readonly displayedArticles = computed<readonly ContentSummary[]>(() => {
    const tab = this.activeTab();
    const filtered = this.articles().filter((article) => {
      if (tab === 'faqs') {
        return article.isFaq === true;
      }

      if (tab === 'guides') {
        return article.contentType === 'Guide' || article.tags.includes('guide');
      }

      if (tab === 'articles') {
        return article.contentType === 'Article' && article.isFaq !== true;
      }

      return true;
    });
    return [...filtered].sort((a, b) => {
      switch (this.sortMode()) {
        case 'views':
          return b.viewCount - a.viewCount;
        case 'status':
          return a.status.localeCompare(b.status);
        default:
          return a.title.localeCompare(b.title);
      }
    });
  });

  readonly statusOptions = computed<readonly DataToolbarOption[]>(() =>
    this.statuses.map((status) => ({ value: status, label: this.locale.t(this.statusLabel(status)) })),
  );

  readonly sortOptions = computed<readonly DataToolbarOption[]>(() => [
    { value: 'title', label: this.locale.t('kb.sort.title') },
    { value: 'views', label: this.locale.t('kb.sort.views') },
    { value: 'status', label: this.locale.t('kb.sort.status') },
  ]);

  readonly listError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  /** Flattened category tree for the dropdown (US-510 AC4). */
  readonly flatCategories = computed<readonly { id: string; name: string }[]>(() => {
    const flat: { id: string; name: string }[] = [];
    const walk = (nodes: readonly CategoryNode[], depth: number): void => {
      for (const node of nodes) {
        flat.push({ id: node.id, name: `${'\u00A0'.repeat(depth * 2)}${node.name}` });
        walk(node.children, depth + 1);
      }
    };
    walk(this.categories(), 0);
    return flat;
  });
  readonly articleTotal = computed(() => this.displayedArticles().length);
  readonly publishedTotal = computed(() => this.displayedArticles().filter((article) => article.status === 'Published').length);
  readonly draftTotal = computed(() => this.displayedArticles().filter((article) => article.status === 'Draft').length);
  readonly publishRate = computed(() => {
    const total = this.articleTotal();
    return total === 0 ? 0 : Math.round((this.publishedTotal() / total) * 100);
  });

  constructor() {
    this.load();
    this.api.categories().subscribe({
      next: (nodes) => this.categories.set(nodes),
      error: () => this.categories.set([]),
    });
  }

  statusLabel(status: string): TranslationKey {
    return STATUS_LABEL_KEYS[status];
  }

  tabLabel(tab: ContentTab): TranslationKey {
    return TAB_LABEL_KEYS[tab];
  }

  load(): void {
    this.state.set(loading());
    const term = this.searchTerm().trim();
    this.api
      .list(this.statusFilter() || undefined, term || undefined)
      .subscribe({
        next: (result) => this.state.set(result.items.length === 0 ? empty() : loaded(result)),
        error: (error: unknown) =>
          this.state.set(
            failed(error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0)),
          ),
      });
  }

  onSearch(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  submitSearch(): void {
    this.load();
  }

  setStatusFilter(status: string): void {
    this.statusFilter.set(status);
    this.load();
  }

  setActiveTab(tab: ContentTab): void {
    this.activeTab.set(tab);
  }

  setSortMode(value: string): void {
    this.sortMode.set(value || 'title');
  }

  // ---- Create / edit (US-510, US-511) -------------------------------------

  openCreate(): void {
    this.editing.set(null);
    this.formTitle.set('');
    this.formSummary.set('');
    this.formBody.set('');
    this.formTags.set('');
    this.authoringType.set(this.activeTab() === 'guides' ? 'Guide' : this.activeTab() === 'faqs' ? 'Faq' : 'Article');
    this.selectedCategoryId.set('');
    this.versions.set([]);
    this.formError.set(null);
    this.formOpen.set(true);
  }

  /** US-511 AC1/AC4 — pre-populated edit; only Drafts are directly editable. */
  openEdit(article: ContentSummary): void {
    if (article.status !== 'Draft') {
      return;
    }
    this.editing.set(article);
    this.formTitle.set(article.title);
    this.formSummary.set(article.summary ?? '');
    this.formBody.set(article.body);
    this.formTags.set(article.tags.join(', '));
    this.authoringType.set(article.isFaq ? 'Faq' : article.contentType === 'Guide' ? 'Guide' : 'Article');
    this.selectedCategoryId.set(article.categoryId ?? '');
    this.formError.set(null);
    this.versions.set([]);
    this.formOpen.set(true);
    this.api.versions(article.id).subscribe({
      next: (entries) => this.versions.set(entries),
      error: () => this.versions.set([]),
    });
  }

  cancelForm(): void {
    this.formOpen.set(false);
    this.editing.set(null);
  }

  save(): void {
    const title = this.formTitle().trim();
    const body = this.formBody().trim();
    if (!title || !body) {
      this.formError.set(null);
      this.formError.set(title ? 'Body is required.' : 'Title is required.');
      return;
    }
    const tags = this.formTags()
      .split(',')
      .map((t) => t.trim())
      .filter((t) => t.length > 0);
    this.saving.set(true);
    this.formError.set(null);

    const request = {
      title,
      body,
      summary: this.formSummary().trim() || null,
      tags,
      contentType: this.authoringType() === 'Guide' ? 'Guide' : 'Article',
    };
    const categoryId = this.selectedCategoryId() || null;
    const done = {
      next: () => {
        this.saving.set(false);
        this.formOpen.set(false);
        this.editing.set(null);
        this.toasts.success(this.locale.t('kb.toast.saved'));
        this.load();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.formError.set(error instanceof ApiError ? error.message_ : 'Something went wrong.');
      },
    };

    const current = this.editing();
    if (current) {
      this.api.update(current.id, request)
        .pipe(
          switchMap(() =>
            categoryId !== (current.categoryId ?? null)
              ? this.api.assignCategory(current.id, categoryId)
              : of(null),
          ),
        )
        .subscribe(done);
    } else {
      this.api.create(request)
        .pipe(switchMap((created) => {
          const categoryCall = categoryId ? this.api.assignCategory(created.id, categoryId) : of(null);
          return categoryCall.pipe(
            switchMap(() => this.authoringType() === 'Faq' ? this.api.setFaq(created.id, true) : of(null)),
          );
        }))
        .subscribe(done);
    }
  }

  // ---- Publish / archive (US-512) -----------------------------------------

  publish(article: ContentSummary): void {
    if (article.status !== 'Draft') {
      return;
    }
    this.mutate(this.api.publish(article.id), this.locale.t('kb.toast.published'));
  }

  /** US-512 AC4 — archive is destructive-ish, so it confirms first. */
  archive(article: ContentSummary): void {
    if (article.status === 'Archived') {
      return;
    }
    this.confirmations
      .confirm({
        title: this.locale.t('kb.archiveConfirm.title'),
        message: this.locale.t('kb.archiveConfirm.body', article.title),
        confirmText: this.locale.t('kb.archive'),
        cancelText: this.locale.t('action.cancel'),
        danger: true,
      })
      .subscribe((accepted) => {
        if (accepted) {
          this.mutate(this.api.archive(article.id), this.locale.t('kb.toast.archived'));
        }
      });
  }

  private mutate(call: Observable<unknown>, successMessage = this.locale.t('kb.toast.updated')): void {
    this.mutating.set(true);
    this.mutationError.set(null);
    call.subscribe({
      next: () => {
        this.mutating.set(false);
        this.toasts.success(successMessage);
        this.load();
      },
      error: (error: unknown) => {
        this.mutating.set(false);
        this.mutationError.set(error instanceof ApiError ? error.message_ : 'Something went wrong.');
      },
    });
  }
}
