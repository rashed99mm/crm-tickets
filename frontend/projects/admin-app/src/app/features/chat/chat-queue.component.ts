import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  ApiError,
  AsyncState,
  ChatApi,
  ChatFilters,
  ChatSessionDto,
  CsButton,
  CsCard,
  CsDataToolbar,
  CsDatePipe,
  CsEmptyState,
  CsErrorState,
  CsLoadingState,
  CsPagination,
  CsStatusPill,
  DataToolbarOption,
  empty,
  failed,
  loaded,
  loading,
  LocaleStore,
  PagedResult,
  TranslatePipe,
} from 'common';

@Component({
  selector: 'admin-chat-queue',
  imports: [
    CsCard,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    CsButton,
    CsStatusPill,
    CsDataToolbar,
    CsPagination,
    TranslatePipe,
    CsDatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './chat-queue.component.html',
})
export default class ChatQueueComponent {
  private readonly api = inject(ChatApi);
  private readonly router = inject(Router);
  protected readonly locale = inject(LocaleStore);

  readonly state = signal<AsyncState<PagedResult<ChatSessionDto>>>(loading());
  readonly claimingId = signal<string | null>(null);
  readonly claimError = signal<ApiError | null>(null);

  readonly page = signal(1);
  readonly pageSize = 10;
  readonly search = signal('');
  readonly status = signal('');
  readonly sortBy = signal('createdAt');
  readonly sortDir = signal('asc');

  readonly statusOptions = computed<readonly DataToolbarOption[]>(() => [
    { value: 'Waiting', label: this.locale.t('chat.queue.statusWaiting') },
    { value: 'Active', label: this.locale.t('chat.queue.statusActive') },
  ]);

  readonly sortOptions = computed<readonly DataToolbarOption[]>(() => [
    { value: 'createdAt', label: this.locale.t('chat.queue.sortByWaitTime') },
    { value: 'customerName', label: this.locale.t('chat.queue.sortByCustomer') },
  ]);

  readonly sessions = computed<readonly ChatSessionDto[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data.items : [];
  });

  readonly totalCount = computed(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data.totalCount : 0;
  });

  readonly hasMore = computed(
    () => this.sessions().length > 0 && this.page() * this.pageSize < this.totalCount(),
  );

  readonly summary = computed(() => {
    const total = this.totalCount();
    const from = (this.page() - 1) * this.pageSize + 1;
    const to = Math.min(this.page() * this.pageSize, total);
    return this.locale.t('pagination.summary', from, to, total);
  });

  readonly listError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set(loading());
    this.claimError.set(null);

    const filters: ChatFilters = {
      page: this.page(),
      pageSize: this.pageSize,
      status: this.status() || undefined,
      search: this.search().trim() || undefined,
      sortBy: this.sortBy(),
      sortDirection: this.sortDir(),
    };

    this.api.getWaitingSessions(filters).subscribe({
      next: (result) => {
        this.state.set(result.items.length === 0 ? empty() : loaded(result));
      },
      error: (err: unknown) => {
        this.state.set(
          failed(
            err instanceof ApiError ? err : new ApiError('ERR_LOAD', 'Failed to load queue', [], '', 0),
          ),
        );
      },
    });
  }

  onSearchChanged(value: string): void {
    this.search.set(value);
  }

  onSearchSubmitted(): void {
    this.page.set(1);
    this.load();
  }

  onStatusChanged(value: string): void {
    this.status.set(value);
    this.page.set(1);
    this.load();
  }

  onSortChanged(value: string): void {
    if (value === this.sortBy()) {
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortBy.set(value);
      this.sortDir.set('asc');
    }
    this.page.set(1);
    this.load();
  }

  goToPage(page: number): void {
    if (page < 1) return;
    this.page.set(page);
    this.load();
  }

  claim(session: ChatSessionDto): void {
    // Claiming is only valid for sessions that are still waiting.
    if (session.status !== 'Waiting' || this.claimingId()) return;

    this.claimingId.set(session.id);
    this.claimError.set(null);

    this.api.claimSession(session.id).subscribe({
      next: () => {
        this.claimingId.set(null);
        this.router.navigate(['/chat/sessions', session.id]);
      },
      error: (err: unknown) => {
        this.claimingId.set(null);
        this.claimError.set(
          err instanceof ApiError ? err : new ApiError('ERR_CLAIM', 'Failed to claim session', [], '', 0),
        );
      },
    });
  }

  openSession(session: ChatSessionDto): void {
    this.router.navigate(['/chat/sessions', session.id]);
  }

  priorityClass(priority: string): string {
    switch (priority) {
      case 'Urgent':
        return 'bg-priority-urgent/10 text-priority-urgent border border-priority-urgent/20';
      case 'High':
        return 'bg-priority-high/10 text-priority-high border border-priority-high/20';
      default:
        return 'bg-priority-normal/10 text-priority-normal border border-priority-normal/20';
    }
  }
}
