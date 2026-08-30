import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ApiError,
  AsyncState,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsLoadingState,
  failed,
  loaded,
  loading,
  ReportDateRangeFilter,
  ReportGroupBy,
  ReportsApi,
  TicketVolumeReport,
  TranslatePipe,
  TranslationKey,
} from 'common';

/** `TranslatePipe` is typed against the dictionary's key union, so a template-side string
 * concatenation (`'reports.groupBy.' + option`) does not type-check — this lookup is the fix. */
const GROUP_BY_LABEL_KEYS: Readonly<Record<ReportGroupBy, TranslationKey>> = {
  day: 'reports.groupBy.day',
  week: 'reports.groupBy.week',
  month: 'reports.groupBy.month',
};

function defaultRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 30);
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
}

/** US-602/US-610 (adapted) — ticket volume by period/category/priority. AC-160, AC-163. */
@Component({
  selector: 'admin-ticket-volume-report',
  imports: [CsCard, CsLoadingState, CsEmptyState, CsErrorState, ReportDateRangeFilter, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ticket-volume-report.component.html',
})
export default class TicketVolumeReportComponent {
  private readonly api = inject(ReportsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly groupByOptions: readonly ReportGroupBy[] = ['day', 'week', 'month'];

  groupByLabel(option: ReportGroupBy): TranslationKey {
    return GROUP_BY_LABEL_KEYS[option];
  }

  protected formatBucketLabel(key: string): string {
    return this.isGuidLike(key) ? 'Uncategorized' : key;
  }

  private isGuidLike(value: string): boolean {
    return /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$/.test(
      value.trim(),
    );
  }

  private readonly initial = {
    from: this.route.snapshot.queryParamMap.get('from') ?? defaultRange().from,
    to: this.route.snapshot.queryParamMap.get('to') ?? defaultRange().to,
    groupBy: (this.route.snapshot.queryParamMap.get('groupBy') as ReportGroupBy) ?? 'day',
  };

  readonly from = signal(this.initial.from);
  readonly to = signal(this.initial.to);
  readonly groupBy = signal<ReportGroupBy>(this.initial.groupBy);

  readonly state = signal<AsyncState<TicketVolumeReport>>(loading());

  readonly report = computed<TicketVolumeReport | null>(() => {
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

  setGroupBy(value: ReportGroupBy): void {
    this.groupBy.set(value);
    this.syncUrl();
    this.load();
  }

  applyRange(range: { from: string; to: string }): void {
    this.from.set(range.from);
    this.to.set(range.to);
    this.syncUrl();
    this.load();
  }

  load(): void {
    this.state.set(loading());
    this.api.ticketVolume({ from: this.from(), to: this.to() }, this.groupBy()).subscribe({
      next: (report) => this.state.set(loaded(report)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  private syncUrl(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { from: this.from(), to: this.to(), groupBy: this.groupBy() },
      queryParamsHandling: 'merge',
    });
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
