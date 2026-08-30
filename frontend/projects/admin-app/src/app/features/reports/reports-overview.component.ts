import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AgentPerformanceReport,
  ApiError,
  AsyncState,
  CsatReport,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  failed,
  loaded,
  loading,
  ReportDateRangeFilter,
  ReportsApi,
  SlaPerformanceReport,
  TicketVolumeReport,
  TranslatePipe,
  TranslationKey,
} from 'common';

function defaultRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 30);
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
}

/** The period key as rendered on a chart bar — "2026-08-01" reads better as "08-01". */
function shortKey(key: string): string {
  return /-W\d+$/.test(key) ? key : key.length > 5 ? key.slice(5) : key;
}

interface ChartBar {
  readonly key: string;
  readonly shortKey: string;
  readonly count: number;
  /** `%` height of the bar relative to its track — 0 when nothing, at least 3% for a visible stub. */
  readonly height: number;
}

/**
 * The reports hub — a "performance overview" (management dashboard) built entirely on the four
 * report endpoints FEAT-20 ships. Each panel keeps its own `AsyncState`: a failing SLA report
 * blanks its own card, never the ticket trend or the leaderboard next to it.
 *
 * Everything rendered here is derived, never fabricated: the last two metrics the dashboard
 * invented (a hardcoded CSAT, hardcoded trend percentages) are the exact numbers this screen was
 * built to replace with `GET /api/reports/*`.
 */
@Component({
  selector: 'admin-reports-overview',
  imports: [RouterLink, CsCard, CsLoadingState, CsEmptyState, CsErrorState, CsIcon, ReportDateRangeFilter, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reports-overview.component.html',
})
export default class ReportsOverviewComponent {
  private readonly api = inject(ReportsApi);

  readonly from = signal(defaultRange().from);
  readonly to = signal(defaultRange().to);

  readonly volume = signal<AsyncState<TicketVolumeReport>>(loading());
  readonly sla = signal<AsyncState<SlaPerformanceReport>>(loading());
  readonly agents = signal<AsyncState<AgentPerformanceReport>>(loading());
  readonly csat = signal<AsyncState<CsatReport>>(loading());

  constructor() {
    this.load();
  }

  applyRange(range: { from: string; to: string }): void {
    this.from.set(range.from);
    this.to.set(range.to);
    this.load();
  }

  /** Four independent requests, four independent panels. No `forkJoin` — one failure must not blank the rest. */
  load(): void {
    this.volume.set(loading());
    this.sla.set(loading());
    this.agents.set(loading());
    this.csat.set(loading());

    const range = { from: this.from(), to: this.to() };
    this.api.ticketVolume(range).subscribe({
      next: (report) => this.volume.set(loaded(report)),
      error: (error: unknown) => this.volume.set(failed(this.toApiError(error))),
    });
    this.api.slaPerformance(range).subscribe({
      next: (report) => this.sla.set(loaded(report)),
      error: (error: unknown) => this.sla.set(failed(this.toApiError(error))),
    });
    this.api.agentPerformance(range).subscribe({
      next: (report) => this.agents.set(loaded(report)),
      error: (error: unknown) => this.agents.set(failed(this.toApiError(error))),
    });
    this.api.csat(range).subscribe({
      next: (report) => this.csat.set(loaded(report)),
      error: (error: unknown) => this.csat.set(failed(this.toApiError(error))),
    });
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }

  protected readonly volumeError = computed<ApiError | null>(() => {
    const current = this.volume();
    return current.status === 'error' ? current.error : null;
  });

  protected readonly agentsError = computed<ApiError | null>(() => {
    const current = this.agents();
    return current.status === 'error' ? current.error : null;
  });

  /** The whole-period ticket count — the sum of every period bucket in the trend. */
  protected readonly totalVolume = computed<number | null>(() => {
    const current = this.volume();
    if (current.status !== 'loaded') {
      return null;
    }
    return current.data.byPeriod.reduce((sum, bucket) => sum + bucket.count, 0);
  });

  /**
   * SLA breach rate across the range, from the met/breached columns of `GET /api/reports/sla-performance`.
   * `met + breached = total` per target type (AC-152), so the denominator is total × 2 target types.
   */
  protected readonly breachRate = computed<number | null>(() => {
    const current = this.sla();
    if (current.status !== 'loaded' || current.data.byPriority.length === 0) {
      return null;
    }
    let total = 0;
    let breached = 0;
    for (const row of current.data.byPriority) {
      total += row.total;
      breached += row.breachedFirstResponse + row.breachedResolution;
    }
    return total === 0 ? null : (breached / (total * 2)) * 100;
  });

  protected readonly breachRateText = computed(() =>
    this.breachRate() === null ? null : `${this.breachRate()!.toFixed(1)}%`,
  );

  /** Mean of the per-agent handle-time approximation the report already computes (spec A7). */
  protected readonly avgResolutionText = computed<string | null>(() => {
    const current = this.agents();
    if (current.status !== 'loaded' || current.data.byAgent.length === 0) {
      return null;
    }
    const minutes =
      current.data.byAgent.reduce((sum, row) => sum + row.avgHandleMinutes, 0) / current.data.byAgent.length;
    const wholeHours = Math.floor(minutes / 60);
    const remaining = Math.round(minutes % 60);
    if (wholeHours <= 0) {
      return `${Math.max(1, remaining)}m`;
    }
    return remaining > 0 ? `${wholeHours}h ${remaining}m` : `${wholeHours}h`;
  });

  protected readonly csatAverageText = computed<string | null>(() => {
    const current = this.csat();
    return current.status === 'loaded' && current.data.totalResponses > 0
      ? current.data.averageRating.toFixed(1)
      : null;
  });

  protected readonly bars = computed<readonly ChartBar[]>(() => {
    const current = this.volume();
    if (current.status !== 'loaded') {
      return [];
    }
    const buckets = current.data.byPeriod;
    const max = buckets.reduce((m, bucket) => Math.max(m, bucket.count), 0);
    return buckets.map((bucket) => ({
      key: bucket.key,
      shortKey: shortKey(bucket.key),
      count: bucket.count,
      height: max === 0 ? 0 : Math.max(3, Math.round((bucket.count / max) * 100)),
    }));
  });

  protected readonly leaderboard = computed(() => {
    const current = this.agents();
    return current.status === 'loaded' ? current.data.byAgent.slice(0, 5) : [];
  });

  /** The five report screens this hub opens — every one also reachable from the sidebar. */
  protected readonly reportLinks: ReadonlyArray<{ path: string; label: TranslationKey }> = [
    { path: '/reports/ticket-volume', label: 'nav.ticketVolume' },
    { path: '/reports/sla-performance', label: 'nav.slaPerformance' },
    { path: '/reports/agent-performance', label: 'nav.agentPerformance' },
    { path: '/reports/csat', label: 'reports.csat.title' },
    { path: '/reports/live-queue', label: 'nav.liveQueue' },
  ];
}