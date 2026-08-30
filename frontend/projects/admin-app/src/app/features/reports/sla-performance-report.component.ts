import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsLoadingState,
  CsIcon,
  failed,
  loaded,
  loading,
  ReportDateRangeFilter,
  ReportsApi,
  SlaPerformanceReport,
  TranslatePipe,
} from 'common';

function defaultRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 30);
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
}

/** US-603/US-610 (adapted) — SLA attainment by priority. AC-161, AC-163. */
@Component({
  selector: 'admin-sla-performance-report',
  imports: [RouterLink, CsCard, CsIcon, CsLoadingState, CsEmptyState, CsErrorState, ReportDateRangeFilter, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sla-performance-report.component.html',
})
export default class SlaPerformanceReportComponent {
  private readonly api = inject(ReportsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly initial = {
    from: this.route.snapshot.queryParamMap.get('from') ?? defaultRange().from,
    to: this.route.snapshot.queryParamMap.get('to') ?? defaultRange().to,
  };

  readonly from = signal(this.initial.from);
  readonly to = signal(this.initial.to);
  readonly state = signal<AsyncState<SlaPerformanceReport>>(loading());

  readonly report = computed<SlaPerformanceReport | null>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : null;
  });

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  readonly totals = computed(() => {
    const rows = this.report()?.byPriority ?? [];
    return rows.reduce(
      (summary, row) => ({
        total: summary.total + row.total,
        responseMet: summary.responseMet + row.metFirstResponse,
        responseBreached: summary.responseBreached + row.breachedFirstResponse,
        resolutionMet: summary.resolutionMet + row.metResolution,
        resolutionBreached: summary.resolutionBreached + row.breachedResolution,
      }),
      { total: 0, responseMet: 0, responseBreached: 0, resolutionMet: 0, resolutionBreached: 0 },
    );
  });

  readonly responseRate = computed(() => this.rate(this.totals().responseMet, this.totals().total));
  readonly resolutionRate = computed(() => this.rate(this.totals().resolutionMet, this.totals().total));

  private rate(met: number, total: number): string {
    return total === 0 ? '0%' : `${Math.round((met / total) * 100)}%`;
  }

  constructor() {
    this.load();
  }

  applyRange(range: { from: string; to: string }): void {
    this.from.set(range.from);
    this.to.set(range.to);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { from: range.from, to: range.to },
      queryParamsHandling: 'merge',
    });
    this.load();
  }

  load(): void {
    this.state.set(loading());
    this.api.slaPerformance({ from: this.from(), to: this.to() }).subscribe({
      next: (report) => this.state.set(loaded(report)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
