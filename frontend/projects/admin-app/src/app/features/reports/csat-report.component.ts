import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ApiError,
  AsyncState,
  CsatReport,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsLoadingState,
  failed,
  loaded,
  loading,
  ReportDateRangeFilter,
  ReportsApi,
  TranslatePipe,
} from 'common';

function defaultRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 30);
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
}

/** US-605 (reopened) — customer satisfaction over a period, from the portal's post-resolution surveys. */
@Component({
  selector: 'admin-csat-report',
  imports: [CsCard, CsLoadingState, CsEmptyState, CsErrorState, ReportDateRangeFilter, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './csat-report.component.html',
})
export default class CsatReportComponent {
  private readonly api = inject(ReportsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly initial = {
    from: this.route.snapshot.queryParamMap.get('from') ?? defaultRange().from,
    to: this.route.snapshot.queryParamMap.get('to') ?? defaultRange().to,
  };

  readonly from = signal(this.initial.from);
  readonly to = signal(this.initial.to);

  readonly state = signal<AsyncState<CsatReport>>(loading());

  readonly report = computed<CsatReport | null>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : null;
  });

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  constructor() {
    this.load();
  }

  applyRange(range: { from: string; to: string }): void {
    this.from.set(range.from);
    this.to.set(range.to);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { from: this.from(), to: this.to() },
      queryParamsHandling: 'merge',
    });
    this.load();
  }

  load(): void {
    this.state.set(loading());
    this.api.csat({ from: this.from(), to: this.to() }).subscribe({
      next: (report) =>
        this.state.set(report.totalResponses === 0 ? { status: 'empty' } : loaded(report)),
      error: (error: unknown) =>
        this.state.set(
          failed(error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0)),
        ),
    });
  }
}
