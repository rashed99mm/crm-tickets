import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  CsStatusPill,
  empty,
  failed,
  loaded,
  loading,
  LocaleStore,
  PortalApi,
  PortalTicketListItem,
  TranslatePipe,
} from 'common';

@Component({
  selector: 'portal-feedback',
  imports: [RouterLink, CsIcon, CsStatusPill, CsLoadingState, CsEmptyState, CsErrorState, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './feedback.component.html',
})
export default class PortalFeedbackComponent {
  private readonly api = inject(PortalApi);
  protected readonly locale = inject(LocaleStore);

  readonly state = signal<AsyncState<readonly PortalTicketListItem[]>>(loading());

  readonly resolvedTickets = computed(() => {
    const current = this.state();
    if (current.status !== 'loaded') {
      return [];
    }
    return current.data.filter((ticket) => ticket.status === 'Resolved' || ticket.status === 'Closed');
  });

  readonly pendingTickets = computed(() => {
    const current = this.state();
    if (current.status !== 'loaded') {
      return [];
    }
    return current.data.filter((ticket) => ticket.status !== 'Resolved' && ticket.status !== 'Closed');
  });

  readonly error = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set(loading());
    this.api.listTickets().subscribe({
      next: (tickets) => this.state.set(tickets.length === 0 ? empty() : loaded(tickets)),
      error: (error: unknown) =>
        this.state.set(
          failed(error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0)),
        ),
    });
  }
}
