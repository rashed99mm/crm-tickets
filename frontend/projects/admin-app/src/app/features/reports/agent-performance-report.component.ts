import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { RouterLink } from '@angular/router';
import {
  AgentPerformanceReport,
  ApiError,
  AsyncState,
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
  TranslatePipe,
  TranslationKey,
} from 'common';

function defaultRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 30);
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
}

/** US-604/US-610 (adapted) — throughput and handle time per agent. AC-162, AC-163. */
@Component({
  selector: 'admin-agent-performance-report',
  imports: [RouterLink, CsCard, CsIcon, CsLoadingState, CsEmptyState, CsErrorState, ReportDateRangeFilter, TranslatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './agent-performance-report.component.html',
})
export default class AgentPerformanceReportComponent {
  private readonly api = inject(ReportsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly initial = {
    from: this.route.snapshot.queryParamMap.get('from') ?? defaultRange().from,
    to: this.route.snapshot.queryParamMap.get('to') ?? defaultRange().to,
  };

  readonly from = signal(this.initial.from);
  readonly to = signal(this.initial.to);
  readonly state = signal<AsyncState<AgentPerformanceReport>>(loading());
  readonly searchTerm = signal('');
  readonly sortMode = signal<'name' | 'resolved' | 'handle'>('resolved');
  readonly page = signal(1);
  readonly pageSize = 10;

  readonly report = computed<AgentPerformanceReport | null>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : null;
  });

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  readonly filteredAgents = computed(() => {
    const term = this.searchTerm().trim().toLocaleLowerCase();
    const rows = (this.report()?.byAgent ?? []).filter((row) => row.agentName.toLocaleLowerCase().includes(term));
    return [...rows].sort((a, b) => {
      switch (this.sortMode()) {
        case 'name': return a.agentName.localeCompare(b.agentName);
        case 'handle': return a.avgHandleMinutes - b.avgHandleMinutes;
        default: return b.ticketsResolved - a.ticketsResolved;
      }
    });
  });

  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.filteredAgents().length / this.pageSize)));
  readonly visibleAgents = computed(() => {
    const start = (Math.min(this.page(), this.pageCount()) - 1) * this.pageSize;
    return this.filteredAgents().slice(start, start + this.pageSize);
  });

  setSearchTerm(value: string): void {
    this.searchTerm.set(value);
    this.page.set(1);
  }

  setSortMode(value: string): void {
    this.sortMode.set(value as 'name' | 'resolved' | 'handle');
    this.page.set(1);
  }

  setPage(page: number): void {
    this.page.set(Math.max(1, Math.min(page, this.pageCount())));
  }

  readonly sortOptions: ReadonlyArray<{ value: 'name' | 'resolved' | 'handle'; label: TranslationKey }> = [
    { value: 'resolved', label: 'reports.agentPerformance.sortResolved' },
    { value: 'name', label: 'reports.agentPerformance.sortName' },
    { value: 'handle', label: 'reports.agentPerformance.sortHandle' },
  ];

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
    this.api.agentPerformance({ from: this.from(), to: this.to() }).subscribe({
      next: (report) => this.state.set(loaded(report)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
