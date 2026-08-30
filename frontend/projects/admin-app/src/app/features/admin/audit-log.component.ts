import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ApiError,
  AsyncState,
  AuditLogApi,
  AuditLogEntry,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  failed,
  fromList,
  loading,
  LocaleStore,
  TranslatePipe,
  CsDatePipe,
} from 'common';

const PAGE_SIZE = 20;

/**
 * FEAT-21 (US-801/US-802) — the audit trail: filter by action type and user, paginated, newest
 * first (the backend's `GetAuditLogQuery` sets that ordering explicitly — see its own comment for
 * why the framework's default would not have given it for free).
 */
@Component({
  selector: 'admin-audit-log',
  imports: [FormsModule, CsCard, CsLoadingState, CsEmptyState, CsErrorState, TranslatePipe, CsDatePipe, CsIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './audit-log.component.html',
})
export default class AuditLogComponent {
  private readonly api = inject(AuditLogApi);

  protected readonly locale = inject(LocaleStore);

  readonly state = signal<AsyncState<readonly AuditLogEntry[]>>(loading());
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly actionType = signal('');
  readonly userId = signal('');
  readonly selected = signal<AuditLogEntry | null>(null);

  readonly entries = computed<readonly AuditLogEntry[]>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : [];
  });

  readonly hasNextPage = computed(() => this.page() * PAGE_SIZE < this.totalCount());
  readonly hasPreviousPage = computed(() => this.page() > 1);

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set(loading());

    this.api
      .list({
        page: this.page(),
        pageSize: PAGE_SIZE,
        actionType: this.actionType().trim() || undefined,
        userId: this.userId().trim() || undefined,
      })
      .subscribe({
        // fromList only ever sees a SUCCESS payload, so an error can never be collapsed into "empty".
        next: (result) => {
          this.totalCount.set(result.totalCount);
          this.state.set(fromList(result.items));
        },
        error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
      });
  }

  applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  nextPage(): void {
    if (!this.hasNextPage()) {
      return;
    }
    this.page.update((p) => p + 1);
    this.load();
  }

  previousPage(): void {
    if (!this.hasPreviousPage()) {
      return;
    }
    this.page.update((p) => p - 1);
    this.load();
  }

  select(entry: AuditLogEntry): void {
    this.selected.set(this.selected() === entry ? null : entry);
  }

  exportCsv(): void {
    const rows = this.entries().map((entry) => [
      entry.createdAt,
      entry.action,
      entry.entityType,
      entry.entityId,
      entry.userName || entry.userId,
      entry.ipAddress || '',
    ]);
    this.downloadCsv('audit-log.csv', [
      ['When', 'Action', 'Entity type', 'Entity id', 'Actor', 'IP address'],
      ...rows,
    ]);
  }

  eventIcon(action: string): string {
    switch (action) {
      case 'Created': return 'add_circle';
      case 'Updated': return 'edit';
      case 'Deleted': return 'delete';
      case 'Login': return 'login';
      default: return 'circle';
    }
  }

  eventColor(action: string): string {
    switch (action) {
      case 'Created': return 'bg-success/10 text-success';
      case 'Updated': return 'bg-primary/10 text-primary';
      case 'Deleted': return 'bg-error/10 text-error';
      case 'Login': return 'bg-tertiary/10 text-tertiary';
      default: return 'bg-surface text-on-surface-variant';
    }
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError
      ? error
      : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }

  private downloadCsv(filename: string, rows: readonly (readonly string[])[]): void {
    const csv = rows.map((row) => row.map((cell) => `"${cell.replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
  }
}
