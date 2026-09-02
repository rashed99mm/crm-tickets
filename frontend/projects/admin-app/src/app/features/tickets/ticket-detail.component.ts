import { ChangeDetectionStrategy, Component, computed, inject, input, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
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
  RESOLUTION_CODES,
  SessionStore,
  SlaCountdown,
  TICKET_IMPACTS,
  TICKET_URGENCIES,
  TicketApi,
  TicketDetail,
  TicketHistoryEntry,
  TicketImpact,
  TicketLink,
  TicketStatus,
  TicketUrgency,
  TranslatePipe,
  CsDatePipe,
} from 'common';
import { AiPanelComponent } from './ai-panel.component';
import { TicketMessagesComponent } from './ticket-messages.component';

type TicketDetailTab = 'info' | 'messages' | 'history' | 'attachments';

/** US-924 — the server refuses the eleventh tag; the UI stops offering one at the same count. */
const MAX_TAGS = 10;

/**
 * US-128 — one ticket, its history, and the actions permitted on it. `AC-61`.
 *
 * The screen that closes `FEAT-06`, `FEAT-07`, `FEAT-08` and (via its `TicketMessagesComponent`
 * child) `FEAT-14`'s user surface.
 */
@Component({
  selector: 'admin-ticket-detail',
  imports: [
    RouterLink,
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
  readonly activeTab = signal<TicketDetailTab>('info');
  readonly referenceCopied = signal(false);
  readonly messages = viewChild(TicketMessagesComponent);

  protected readonly resolutionCodes = RESOLUTION_CODES;
  protected readonly impacts = TICKET_IMPACTS;
  protected readonly urgencies = TICKET_URGENCIES;
  readonly showResolveForm = signal(false);
  readonly newTagValue = signal('');
  readonly newLinkType = signal<'RelatedTo' | 'DuplicateOf'>('RelatedTo');
  readonly newLinkReference = signal('');

  readonly ticket = computed<TicketDetail | null>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : null;
  });

  /**
   * Whether the ticket has hit the tag ceiling.
   *
   * A computed rather than `t.tags.length >= MAX_TAGS` inline, because a `>` inside a template
   * attribute breaks any naive `<[^>]*>` tag stripper — including `no-hardcoded-strings.spec.ts`'s
   * AC-63 sweep, which then reports the rest of the element as untranslated visible text.
   */
  readonly tagLimitReached = computed(() => (this.ticket()?.tags.length ?? 0) >= MAX_TAGS);

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
   * The header's reference chip doubles as a copy button — the reference is what an agent pastes
   * into a chat, a call note or another ticket, and selecting six characters of mono text by hand
   * is the kind of friction that gets a UI called clunky.
   *
   * `navigator.clipboard` is absent on insecure origins and in some test environments, so the
   * failure path is silent: the chip simply does not flip to "Copied", which is honest — claiming
   * a copy that did not happen is worse than no feedback.
   */
  copyReference(reference: string): void {
    void navigator.clipboard
      ?.writeText(reference)
      .then(() => {
        this.referenceCopied.set(true);
        setTimeout(() => this.referenceCopied.set(false), 1500);
      })
      .catch(() => {
        // Clipboard permission refused. Leave the chip as it was.
      });
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

  /**
   * AC-922.7: `Resolved` is never committed bare. Selecting it opens the inline form instead of
   * calling the API — `submitResolve` is what actually posts. Every other target still commits
   * immediately, matching the existing one-click behaviour AC-61 already established.
   */
  selectStatus(status: string): void {
    if (!status) {
      return;
    }

    if (status === 'Resolved') {
      this.showResolveForm.set(true);
      return;
    }

    this.commitStatus(status);
  }

  submitResolve(resolutionCode: string, resolutionNotes: string): void {
    const current = this.ticket();
    if (!current || this.busy() || !resolutionCode || !resolutionNotes.trim()) {
      return;
    }

    this.run(
      this.api.changeStatus(current.id, 'Resolved', current.rowVersion, resolutionCode, resolutionNotes),
    );
    this.showResolveForm.set(false);
  }

  cancelResolve(): void {
    this.showResolveForm.set(false);
  }

  reclassify(impact: string, urgency: string): void {
    const current = this.ticket();
    if (!current || this.busy() || !impact || !urgency) {
      return;
    }

    this.run(this.api.reclassify(current.id, impact as TicketImpact, urgency as TicketUrgency, current.rowVersion));
  }

  addTag(value: string): void {
    const current = this.ticket();
    const trimmed = value.trim();
    if (!current || this.busy() || !trimmed) {
      return;
    }

    this.run(this.api.addTag(current.id, trimmed));
    this.newTagValue.set('');
  }

  removeTag(value: string): void {
    const current = this.ticket();
    if (!current || this.busy()) {
      return;
    }

    this.run(this.api.removeTag(current.id, value));
  }

  addLink(linkType: string, targetReference: string): void {
    const current = this.ticket();
    const reference = targetReference.trim();
    if (!current || this.busy() || !reference) {
      return;
    }

    this.run(this.api.addLink(current.id, linkType as 'RelatedTo' | 'DuplicateOf', reference));
    this.newLinkReference.set('');
  }

  removeLink(linkId: string): void {
    const current = this.ticket();
    if (!current || this.busy()) {
      return;
    }

    this.run(this.api.removeLink(current.id, linkId));
  }

  /**
   * AC-925.5 — the directional reading. `RelatedTo` shows the same way from both sides; `DuplicateOf`
   * does not: the source reads "duplicate of", the target it points at reads "duplicated by".
   */
  linkLabel(link: TicketLink): string {
    if (link.linkType === 'RelatedTo') {
      return this.locale.t('tickets.detail.links.related');
    }

    return link.direction === 'Outbound'
      ? this.locale.t('tickets.detail.links.duplicateOf')
      : this.locale.t('tickets.detail.links.duplicatedBy');
  }

  private commitStatus(status: string): void {
    const current = this.ticket();
    if (!current || this.busy()) {
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
