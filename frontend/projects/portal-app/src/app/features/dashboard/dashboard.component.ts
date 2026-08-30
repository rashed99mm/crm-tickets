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
  SessionStore,
  TranslatePipe,
} from 'common';

@Component({
  selector: 'portal-dashboard',
  imports: [RouterLink, CsIcon, CsStatusPill, CsLoadingState, CsEmptyState, CsErrorState, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.component.html',
})
export default class PortalDashboardComponent {
  private readonly api = inject(PortalApi);
  protected readonly session = inject(SessionStore);
  protected readonly locale = inject(LocaleStore);

  readonly state = signal<AsyncState<readonly PortalTicketListItem[]>>(loading());

  readonly tickets = computed<readonly PortalTicketListItem[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : [];
  });

  readonly openCount = computed(
    () => this.tickets().filter((ticket) => ticket.status !== 'Resolved' && ticket.status !== 'Closed').length,
  );

  readonly resolvedCount = computed(
    () => this.tickets().filter((ticket) => ticket.status === 'Resolved' || ticket.status === 'Closed').length,
  );

  readonly recentTickets = computed(() => this.tickets().slice(0, 4));

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
