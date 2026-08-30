import { ChangeDetectionStrategy, Component, computed, inject, input, signal, viewChild } from '@angular/core';
import {
  ApiError,
  AssignableAgent,
  AsyncState,
  CsBadge,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  CsPlaceholder,
  CsAttachmentList,
  failed,
  loaded,
  loading,
  LocaleStore,
  PERMITTED_TRANSITIONS,
  SessionStore,
  SlaCountdown,
  TicketApi,
  TicketDetail,
  TicketHistoryEntry,
  TicketStatus,
  TranslatePipe,
  CsDatePipe,
} from 'common';
import { AiPanelComponent } from './ai-panel.component';
import { TicketMessagesComponent } from './ticket-messages.component';

type TicketDetailTab = 'messages' | 'history' | 'attachments';

/**
 * US-128 — one ticket, its history, and the actions permitted on it. `AC-61`.
 *
 * The screen that closes `FEAT-06`, `FEAT-07`, `FEAT-08` and (via its `TicketMessagesComponent`
 * child) `FEAT-14`'s user surface.
 */
@Component({
  selector: 'admin-ticket-detail',
  imports: [
    CsCard,
    CsIcon,
    CsBadge,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    CsPlaceholder,
    CsAttachmentList,
    TicketMessagesComponent,
    AiPanelComponent,
    TranslatePipe,
    SlaCountdown,

    CsDatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ticket-detail.component.html',
})
export default class TicketDetailComponent {
  private readonly api = inject(TicketApi);
  private readonly session = inject(SessionStore);

  protected readonly locale = inject(LocaleStore);

  /** Bound from the route via `withComponentInputBinding`. */
  readonly id = input.required<string>();

  readonly state = signal<AsyncState<TicketDetail>>(loading());
  readonly busy = signal(false);
  readonly actionError = signal<ApiError | null>(null);
  readonly agents = signal<readonly AssignableAgent[]>([]);
  readonly activeTab = signal<TicketDetailTab>('messages');
  readonly messages = viewChild(TicketMessagesComponent);

  readonly ticket = computed<TicketDetail | null>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : null;
  });

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  /**
   * Who opened the ticket, for the header band's byline.
   *
   * `TicketDetailDto` has no `reportedBy` field; the only record of the opener is the `Created`
   * entry in `history`, so it is read from there. Returns null rather than a fallback string when
   * that entry is not in the page returned — the band then shows the placeholder, which is honest,
   * where "Unknown" would read as a name the server actually sent.
   */
  readonly openedBy = computed<string | null>(
    () => this.ticket()?.history.find((entry) => entry.changeType === 'Created')?.actorName ?? null,
  );

  /**
   * The timeline marker's glyph, by what happened.
   *
   * `ticket_detail_chatbot` gives each timeline entry a glyph for its kind rather than an anonymous
   * dot, which is what lets an agent find the reopen in a history of twenty status changes without
   * reading every line. A `switch` with a default rather than a lookup keyed by the union: the
   * server can add a change type before this file learns about it, and an unknown one is a neutral
   * glyph, not a crash.
   */
  historyGlyph(entry: TicketHistoryEntry): string {
    switch (entry.changeType) {
      case 'Created':
        return 'add_circle';
      case 'Assigned':
        return 'person_add';
      case 'Reassigned':
        return 'swap_horiz';
      case 'StatusChanged':
        return 'sync_alt';
      case 'Reopened':
        return 'restart_alt';
      default:
        return 'history';
    }
  }

  setTab(tab: TicketDetailTab): void {
    this.activeTab.set(tab);
  }

  /**
   * AC-61's hidden half. **This is a courtesy, not the control** — the server refuses an agent's
   * assign with 403 regardless of what this renders (AC-43), and that is what
   * `AC43_Agent_AssigningAnyTicket_Returns403` proves. Hiding a control the caller may not use is
   * about not offering people dead ends.
   */
  readonly canAssign = computed(
    () => this.session.hasRole('Supervisor') || this.session.hasRole('Admin'),
  );
  readonly canTakeEscalation = computed(() => {
    const current = this.ticket();
    return this.canAssign() && !!current && current.escalationState !== 'None';
  });

  /** Only the transitions the server would accept from where the ticket actually is (AC-37). */
  readonly availableTransitions = computed<readonly TicketStatus[]>(() => {
    const current = this.ticket();
    return current ? PERMITTED_TRANSITIONS[current.status] : [];
  });

  constructor() {
    // Deliberately not in an effect: the id is a route input that does not change while this
    // component is alive, and an effect would re-fire on every unrelated signal write.
    queueMicrotask(() => this.load());
  }

  load(): void {
    this.state.set(loading());
    this.actionError.set(null);

    this.api.get(this.id()).subscribe({
      next: (ticket) => {
        this.state.set(loaded(ticket));
        if (this.canAssign() && this.agents().length === 0) {
          this.loadAgents();
        }
      },
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  private loadAgents(): void {
    this.api.listAssignableAgents().subscribe({
      // A picker that failed to load is not an action failure, and surfacing it as one would claim
      // something was attempted that never was.
      next: (agents) => this.agents.set(agents),
      error: () => this.agents.set([]),
    });
  }

  changeStatus(status: string): void {
    const current = this.ticket();
    if (!current || this.busy() || !status) {
      return;
    }

    this.run(this.api.changeStatus(current.id, status as TicketStatus, current.rowVersion));
  }

  assign(assigneeId: string): void {
    const current = this.ticket();
    if (!current || this.busy() || !assigneeId) {
      return;
    }

    this.run(this.api.assign(current.id, assigneeId, current.rowVersion));
  }

  takeEscalation(assigneeId: string): void {
    const current = this.ticket();
    if (!current || this.busy() || !assigneeId) {
      return;
    }

    this.run(this.api.takeEscalation(current.id, assigneeId, current.rowVersion));
  }

  /**
   * Every mutation re-reads on success **and on failure**.
   *
   * On a 409 the local `rowVersion` is stale by definition, so patching the local copy would leave
   * the screen holding a version the server has already superseded and the next attempt would fail
   * identically. Re-reading is the only honest recovery; the server's message says why.
   */
  private run(work: {
    subscribe(observer: { next: () => void; error: (e: unknown) => void }): unknown;
  }): void {
    this.busy.set(true);
    this.actionError.set(null);

    work.subscribe({
      next: () => {
        this.busy.set(false);
        this.load();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.actionError.set(this.toApiError(error));
        this.reloadPreservingError();
      },
    });
  }

  /** Refreshes the ticket without clearing the message that explains the refusal. */
  private reloadPreservingError(): void {
    const message = this.actionError();

    this.api.get(this.id()).subscribe({
      next: (ticket) => {
        this.state.set(loaded(ticket));
        this.actionError.set(message);
      },
      error: () => {
        // The mutation's error is the more useful of the two; leave it standing.
      },
    });
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
