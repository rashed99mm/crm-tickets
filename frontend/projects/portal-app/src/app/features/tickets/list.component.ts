import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  CsStatusPill,
  empty,
  failed,
  idle,
  loaded,
  loading,
  LocaleStore,
  PortalApi,
  PortalTicketListItem,
  TranslatePipe,
} from 'common';

/** `ticket_queue` — the customer's own tickets, read-only list (US-405, PJ-8). Unpaged. */
@Component({
  selector: 'portal-ticket-list',
  imports: [
    RouterLink,
    CsCard,
    CsIcon,
    CsStatusPill,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './list.component.html',
})
export default class PortalTicketListComponent {
  private readonly api = inject(PortalApi);
  protected readonly locale = inject(LocaleStore);

  readonly state = signal<AsyncState<readonly PortalTicketListItem[]>>(loading());

  readonly tickets = computed<readonly PortalTicketListItem[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : [];
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
    this.api.listTickets().subscribe({
      next: (items) => this.state.set(items.length === 0 ? empty() : loaded(items)),
      error: (error: unknown) =>
        this.state.set(
          failed(error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0)),
        ),
    });
  }
}
