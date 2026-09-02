import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  CsBadge,
  CsCard,
  CsChannelPill,
  CsDataToolbar,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  CsPagination,
  CsSlaPill,
  CsStatusPill,
  PagedResult,
  SlaVisualState,
  empty,
  failed,
  loaded,
  loading,
  LocaleStore,
  TICKET_STATUSES,
  TicketApi,
  TicketListItem,
  TicketStatus,
  DataToolbarOption,
  TranslatePipe,
} from 'common';

/**
 * US-038 and US-126 — the queue. `AC-57` (paged, status filter, "my tickets") and `AC-58` (loading,
 * empty and error visually distinct).
 *
 * The list is an `AsyncState` union rather than an array plus a loading flag. That is the whole of
 * AC-58's defence: with "data or nothing", `catchError(() => of([]))` looks reasonable, and it
 * turns a server outage into "no tickets" — the user reports missing work, nobody looks for a
 * fault, and the outage stays invisible.
 */
@Component({
  selector: 'admin-ticket-queue',
  imports: [
    RouterLink,
    CsCard,
    CsIcon,
    CsChannelPill,
    CsBadge,
    CsStatusPill,
    CsSlaPill,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    CsPagination,
    CsDataToolbar,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ticket-queue.component.html',
})
export default class TicketQueueComponent {
  private readonly api = inject(TicketApi);
  private readonly locale = inject(LocaleStore);

  protected readonly statuses = TICKET_STATUSES;

  readonly state = signal<AsyncState<PagedResult<TicketListItem>>>(loading());
  readonly status = signal<TicketStatus | null>(null);
  readonly mine = signal(false);
  readonly page = signal(1);
  readonly search = signal('');
  readonly tagFilter = signal('');

  readonly escalationSort = signal(false);

  readonly statusOptions = computed<readonly DataToolbarOption[]>(() =>
    this.statuses.map((status) => ({ value: status, label: status })),
  );

  readonly sortMode = computed(() => (this.escalationSort() ? 'escalation' : 'newest'));

  readonly sortOptions = computed<readonly DataToolbarOption[]>(() => [
    { value: 'newest', label: this.locale.t('tickets.queue.sortNewest') },
    { value: 'escalation', label: this.locale.t('tickets.queue.sortByEscalation') },
  ]);

  // Angular templates do not narrow a discriminated union across a @switch, so the two
  // payload-carrying cases are projected into typed signals here.
  readonly tickets = computed<readonly TicketListItem[]>(() => {
    const current = this.state();
    const items = current.status === 'loaded' ? current.data.items : [];
    const query = this.search().trim().toLocaleLowerCase();
    const filtered = query
      ? items.filter((ticket) =>
          [
            ticket.reference,
            ticket.subject,
            ticket.customerName,
            ticket.categoryName,
            ticket.priority,
            ticket.status,
            ticket.assigneeName ?? '',
          ]
            .join(' ')
            .toLocaleLowerCase()
            .includes(query),
        )
      : items;
    if (!this.escalationSort()) {
      return filtered;
    }
    // A7 — client-side re-order of the currently loaded page only; the server always orders by
    // CreatedAt and this does not ask it for a second sort dimension.
    return [...filtered].sort(
      (a, b) => Number(b.escalationState !== 'None') - Number(a.escalationState !== 'None'),
    );
  });

  readonly loadedItems = computed<readonly TicketListItem[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data.items : [];
  });

  readonly totalCount = computed(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data.totalCount : 0;
  });

  readonly visibleCount = computed(() => this.tickets().length);

  readonly openCount = computed(
    () =>
      this.loadedItems().filter(
        (ticket) => ticket.status !== 'Resolved' && ticket.status !== 'Closed',
      ).length,
  );

  readonly escalatedCount = computed(
    () => this.loadedItems().filter((ticket) => ticket.escalationState !== 'None').length,
  );

  readonly unassignedCount = computed(
    () => this.loadedItems().filter((ticket) => !ticket.assigneeId).length,
  );

  readonly searchHasNoMatches = computed(
    () =>
      this.state().status === 'loaded' &&
      this.search().trim().length > 0 &&
      this.loadedItems().length > 0 &&
      this.tickets().length === 0,
  );

  readonly listError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  /**
   * "No tickets" under an active filter tells the user their queue is empty when it is not. The
   * fix is copy, not logic.
   */
  readonly emptyMessage = computed(() =>
    this.locale.t(this.status() || this.mine() ? 'tickets.empty.filtered' : 'tickets.empty.all'),
  );

  readonly hasMore = computed(
    () => this.tickets().length > 0 && this.page() * 10 < this.totalCount(),
  );

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set(loading());

    this.api
      .list({
        page: this.page(),
        pageSize: 10,
        status: this.status(),
        mine: this.mine(),
        tag: this.tagFilter() || null,
      })
      .subscribe({
        // `empty` only ever describes a SUCCESSFUL request that returned nothing. An error can
        // never reach this branch, which is what keeps AC-58's two states distinct.
        next: (result) => this.state.set(result.items.length === 0 ? empty() : loaded(result)),
        error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
      });
  }

  selectStatus(value: string): void {
    this.status.set(value === '' ? null : (value as TicketStatus));
    this.page.set(1);
    this.load();
  }

  toggleMine(): void {
    this.mine.set(!this.mine());
    this.page.set(1);
    this.load();
  }

  setTagFilter(value: string): void {
    this.tagFilter.set(value);
    this.page.set(1);
    this.load();
  }

  updateSearch(value: string): void {
    this.search.set(value);
  }

  goToPage(page: number): void {
    if (page < 1) {
      return;
    }

    this.page.set(page);
    this.load();
  }

  sortByEscalation(): void {
    this.escalationSort.set(!this.escalationSort());
  }

  setSortMode(value: string): void {
    this.escalationSort.set(value === 'escalation');
  }

  escalationLabel(ticket: TicketListItem): string | null {
    switch (ticket.escalationState) {
      case 'Level1':
        return this.locale.t('tickets.escalation.level1');
      case 'Level2':
        return this.locale.t('tickets.escalation.level2');
      case 'Level3':
        return this.locale.t('tickets.escalation.level3');
      default:
        return null;
    }
  }

  channel(ticket: TicketListItem): string {
    return ticket.channel ?? 'WebForm';
  }

  assigneeLabel(ticket: TicketListItem): string {
    return ticket.assigneeName?.trim() || this.locale.t('field.notRecorded');
  }

  slaState(ticket: TicketListItem): SlaVisualState {
    if (ticket.escalationState && ticket.escalationState !== 'None') {
      return 'breached';
    }

    const dueAt = ticket.responseDueAt ?? ticket.resolutionDueAt;
    if (!dueAt) {
      return 'unavailable';
    }

    const due = Date.parse(dueAt);
    if (Number.isNaN(due)) {
      return 'unavailable';
    }

    const hoursRemaining = (due - Date.now()) / 3_600_000;
    if (hoursRemaining < 0) {
      return 'breached';
    }

    return hoursRemaining <= 4 ? 'warning' : 'healthy';
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError
      ? error
      : new ApiError(
          'ERR_UNKNOWN',
          'Something went wrong',
          [],
          '',
          0,
        );
  }
}
