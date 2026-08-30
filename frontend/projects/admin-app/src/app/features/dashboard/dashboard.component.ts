import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  ApiError,
  AsyncState,
  CsCard,
  CsDatePipe,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsStatCard,
  CsStatusPill,
  CsLoadingState,
  CsatReport,
  AgentPerformanceReport,
  TicketVolumeReport,
  empty,
  failed,
  idle,
  loaded,
  loading,
  ReportsApi,
  SessionStore,
  TicketApi,
  TicketListItem,
  ToastService,
  PagedResult,
  TicketStatus,
  LocaleStore,
  TranslatePipe,
} from 'common';

/**
 * The statuses the dashboard counts.
 *
 * `A17` — `Resolved` and `Closed` are not "my open work". Counting them would inflate the number
 * an agent reads as their workload, which is the opposite of what AC-78 is for.
 */
const COUNTED_STATUSES = ['New', 'Open', 'Assigned', 'In Progress', 'Waiting for Customer', 'Waiting for Internal Team', 'Resolved', 'Closed'] as const satisfies readonly TicketStatus[];

/** A tile: the status and how many of mine are in it. */
export interface StatusCount {
  readonly status: TicketStatus;
  readonly count: number;
}

/** Ten rows — the shape of the day, not a second queue. Paging lives on `/tickets`. */
const MY_WORK_PAGE_SIZE = 10;

/** The CSAT tile's window — the last thirty days, matching every report screen's default range. */
const CSAT_RANGE_DAYS = 30;
const DASHBOARD_REPORT_RANGE_DAYS = 30;

/**
 * MVP-12 — the agent dashboard. `AC-77`…`AC-82`.
 *
 * Composed from the ticket endpoints and the existing report endpoints; there is no dashboard endpoint. Four small round
 * trips against building a purpose-made aggregate is a deliberate trade recorded in the spec, not
 * an oversight — see `countOnly` in `ticket.api.ts`.
 *
 * The panels hold **separate** `AsyncState` signals rather than one state for the screen. A single
 * state would mean a failing status count blanks the list of work, which is a worse screen than
 * either panel alone: the agent would lose their tickets because a tile could not be counted.
 */
@Component({
  selector: 'admin-dashboard',
  imports: [
    RouterLink,
    CsCard,
    CsIcon,
    CsStatCard,
    CsStatusPill,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    TranslatePipe,
    CsDatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.component.html',
})
export default class DashboardComponent {
  /**
   * The glyph beside each metric tile's label, as the mockups' metric cards carry one.
   *
   * Presentation only — it changes nothing about which statuses are counted, which is
   * `COUNTED_STATUSES` above. A status with no entry falls back to `inbox` rather than rendering
   * an empty box, for the same reason the badge has a fallback tone: the backend may add a status
   * before this file learns about it.
   */
  protected readonly tileIcon: Partial<Record<TicketStatus, string>> = {
    New: 'note_add',
    Open: 'folder_open',
    Assigned: 'person_add',
    'In Progress': 'pending',
    'Waiting for Customer': 'hourglass_empty',
    'Waiting for Internal Team': 'engineering',
    Resolved: 'task_alt',
    Closed: 'archive',
  };

  private readonly api = inject(TicketApi);
  private readonly reportsApi = inject(ReportsApi);
  private readonly session = inject(SessionStore);
  private readonly locale = inject(LocaleStore);
  private readonly toast = inject(ToastService);

  readonly myWork = signal<AsyncState<PagedResult<TicketListItem>>>(loading()); // AC-77, AC-80, AC-81
  readonly counts = signal<AsyncState<readonly StatusCount[]>>(loading()); // AC-78

  /**
   * `idle`, not `loading` — for an agent this panel is never requested at all, and `loading` would
   * render a spinner for work that will never arrive (AC-82).
   */
  readonly unassigned = signal<AsyncState<number>>(idle());

  /**
   * The team's CSAT over the last thirty days, straight from `GET /api/reports/csat`. The tile used
   * to show a fabricated `4.8` to everyone; now it shows the real number to the people the report
   * is for, and `idle` (never even requested) to everyone else.
   */
  readonly csat = signal<AsyncState<CsatReport>>(idle());

  /** Supervisor-only report data used by the Stitch trend and top-agent panels. */
  readonly ticketVolume = signal<AsyncState<TicketVolumeReport>>(idle());
  readonly agentPerformance = signal<AsyncState<AgentPerformanceReport>>(idle());

  /**
   * A courtesy, not the control. The server decides what a supervisor may see; this only avoids
   * asking for a number the session has no business reading.
   */
  readonly isSupervisor = computed(
    () => this.session.hasRole('Supervisor') || this.session.hasRole('Admin'),
  );

  // Angular templates do not narrow a discriminated union across a @switch, so the
  // payload-carrying cases are projected into typed signals here.
  readonly tickets = computed<readonly TicketListItem[]>(() => {
    const current = this.myWork();
    return current.status === 'loaded' ? current.data.items : [];
  });

  readonly quickReplies = [
    'dashboard.quickReplies.investigating',
    'dashboard.quickReplies.needInfo',
    'dashboard.quickReplies.resolved',
  ] as const;

  readonly peakVolume = computed<number | null>(() => {
    const current = this.ticketVolume();
    if (current.status !== 'loaded' || current.data.byPeriod.length === 0) {
      return null;
    }

    return Math.max(...current.data.byPeriod.map((bucket) => bucket.count));
  });

  readonly topAgents = computed(() => {
    const current = this.agentPerformance();
    return current.status === 'loaded' ? current.data.byAgent.slice(0, 4) : [];
  });

  agentInitials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('');
  }

  async copyQuickReply(reply: (typeof this.quickReplies)[number]): Promise<void> {
    const text = this.locale.t(reply);
    try {
      await navigator.clipboard.writeText(text);
      this.toast.success(this.locale.t('workspace.quickReplyCopied'), text);
    } catch {
      this.toast.error(this.locale.t('workspace.quickReplyCopyFailed'));
    }
  }

  readonly myWorkError = computed<ApiError | null>(() => {
    const current = this.myWork();
    return current.status === 'error' ? current.error : null;
  });

  readonly statusCounts = computed<readonly StatusCount[]>(() => {
    const current = this.counts();
    return current.status === 'loaded' ? current.data : [];
  });

  /** AC-A1 — one bento tile per counted status plus a "my open" total. */
  readonly myOpenTotal = computed(() =>
    this.statusCounts().reduce((sum, tile) => sum + tile.count, 0),
  );

  /** `agent_dashboard_overview`'s metric tile: a coloured icon chip per status. */
  readonly statIconTone: Partial<Record<TicketStatus, string>> = {
    New: 'bg-surface-container-high text-primary',
    Open: 'bg-surface-container-high text-primary',
    Assigned: 'bg-surface-container-high text-primary',
    'In Progress': 'bg-surface-container-high text-primary',
    'Waiting for Customer': 'bg-surface-container-high text-primary',
    'Waiting for Internal Team': 'bg-surface-container-high text-primary',
    Resolved: 'bg-surface-container-high text-primary',
    Closed: 'bg-surface-container-high text-primary',
  };

  readonly statLabel: Partial<
    Record<TicketStatus, 'dashboard.stat.new' | 'dashboard.stat.open' | 'dashboard.stat.assigned' | 'dashboard.stat.in_progress' | 'dashboard.stat.waiting_for_customer' | 'dashboard.stat.waiting_for_internal_team' | 'dashboard.stat.resolved' | 'dashboard.stat.closed'>
  > = {
    New: 'dashboard.stat.new',
    Open: 'dashboard.stat.open',
    Assigned: 'dashboard.stat.assigned',
    'In Progress': 'dashboard.stat.in_progress',
    'Waiting for Customer': 'dashboard.stat.waiting_for_customer',
    'Waiting for Internal Team': 'dashboard.stat.waiting_for_internal_team',
    Resolved: 'dashboard.stat.resolved',
    Closed: 'dashboard.stat.closed',
  };

  readonly countsError = computed<ApiError | null>(() => {
    const current = this.counts();
    return current.status === 'error' ? current.error : null;
  });

  readonly openTicketsCount = computed(() => {
    const counts = this.statusCounts();
    const open = counts.find((c) => c.status === 'Open');
    const newC = counts.find((c) => c.status === 'New');
    return (open?.count ?? 0) + (newC?.count ?? 0);
  });

  readonly pendingCount = computed(() => {
    const counts = this.statusCounts();
    const waiting = counts.find((c) => c.status === 'Waiting for Customer');
    const pending = counts.find((c) => c.status === 'Waiting for Internal Team');
    return (waiting?.count ?? 0) + (pending?.count ?? 0);
  });

  readonly resolvedCount = computed(() => {
    const counts = this.statusCounts();
    const resolved = counts.find((c) => c.status === 'Resolved');
    const closed = counts.find((c) => c.status === 'Closed');
    return (resolved?.count ?? 0) + (closed?.count ?? 0);
  });

  readonly unassignedCount = computed(() => {
    const current = this.unassigned();
    return current.status === 'loaded' ? current.data : 0;
  });

  /**
   * The CSAT figure, formatted for the tile — or `null` when there is nothing to report. Zero
   * responses is a fact worth displaying as a dash rather than the `4.8` the screen used to invent.
   */
  readonly csatAverage = computed<string | null>(() => {
    const current = this.csat();
    return current.status === 'loaded' && current.data.totalResponses > 0
      ? current.data.averageRating.toFixed(1)
      : null;
  });

  readonly unassignedError = computed<ApiError | null>(() => {
    const current = this.unassigned();
    return current.status === 'error' ? current.error : null;
  });

  constructor() {
    this.loadMyWork();
    this.loadCounts();

    // Guarded here rather than in the template. A hidden tile whose request still went out is
    // still visible in the network tab, so the check has to sit before the call (AC-82).
    if (this.isSupervisor()) {
      this.loadUnassigned();
      this.loadCsat();
      this.loadTicketVolume();
      this.loadAgentPerformance();
    }
  }

  private reportRange() {
    const to = new Date();
    const from = new Date(to);
    from.setDate(from.getDate() - DASHBOARD_REPORT_RANGE_DAYS);
    return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
  }

  loadTicketVolume(): void {
    this.ticketVolume.set(loading());
    this.reportsApi.ticketVolume(this.reportRange()).subscribe({
      next: (report) => this.ticketVolume.set(loaded(report)),
      error: (error: unknown) => this.ticketVolume.set(failed(this.toApiError(error))),
    });
  }

  loadAgentPerformance(): void {
    this.agentPerformance.set(loading());
    this.reportsApi.agentPerformance(this.reportRange()).subscribe({
      next: (report) => this.agentPerformance.set(loaded(report)),
      error: (error: unknown) => this.agentPerformance.set(failed(this.toApiError(error))),
    });
  }

  /**
   * The CSAT report is `Supervisor`-policy-gated on the backend, so it is requested only when the
   * session is allowed to read it — the same guard as `loadUnassigned`, before the call.
   */
  loadCsat(): void {
    const to = new Date();
    const from = new Date(to);
    from.setDate(from.getDate() - CSAT_RANGE_DAYS);
    this.csat.set(loading());
    this.reportsApi
      .csat({ from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) })
      .subscribe({
        next: (report) => this.csat.set(loaded(report)),
        error: (error: unknown) => this.csat.set(failed(this.toApiError(error))),
      });
  }

  /** `AC-77` — my work, newest first as the server orders it. No client-side re-sort. */
  loadMyWork(): void {
    this.myWork.set(loading());

    this.api.list({ page: 1, pageSize: MY_WORK_PAGE_SIZE, mine: true }).subscribe({
      // `empty` only ever describes a SUCCESSFUL request that returned nothing. An error can
      // never reach this branch, which is what keeps AC-80 and AC-81 distinct.
      next: (result) => this.myWork.set(result.items.length === 0 ? empty() : loaded(result)),
      error: (error: unknown) => this.myWork.set(failed(this.toApiError(error))),
    });
  }

  /**
   * `AC-78` — one `totalCount` per counted status.
   *
   * `forkJoin` because the three tiles are read as a set: a screen showing two of three numbers
   * misrepresents the workload more than one showing none. They fail and retry together.
   */
  loadCounts(): void {
    this.counts.set(loading());

    forkJoin(
      COUNTED_STATUSES.map((status) => this.api.countOnly({ mine: true, status })),
    ).subscribe({
      next: (totals) =>
        this.counts.set(
          loaded(COUNTED_STATUSES.map((status, index) => ({ status, count: totals[index] }))),
        ),
      error: (error: unknown) => this.counts.set(failed(this.toApiError(error))),
    });
  }

  /** `AC-82` — supervisors only. Never called for an agent, so no request is ever issued. */
  loadUnassigned(): void {
    this.unassigned.set(loading());

    this.api.countOnly({ unassigned: true }).subscribe({
      // Zero unassigned tickets is a number worth showing, not an empty state — "0 waiting" is
      // the good news a supervisor opens this screen for.
      next: (total) => this.unassigned.set(loaded(total)),
      error: (error: unknown) => this.unassigned.set(failed(this.toApiError(error))),
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
