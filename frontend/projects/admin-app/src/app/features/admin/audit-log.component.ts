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
  TranslationKey,
  CsDatePipe,
} from 'common';

const PAGE_SIZE = 20;

/** Matches `AuditBehavior.cs`'s `action` values exactly (`Created`/`Updated`/`Deleted`/`Login`). */
const ACTION_LABELS: Readonly<Record<string, TranslationKey>> = {
  Created: 'auditLog.action.created',
  Updated: 'auditLog.action.updated',
  Deleted: 'auditLog.action.deleted',
  Login: 'auditLog.action.login',
};

/** Matches `AuditBehavior.cs`'s `EntityTypeMapping` values, plus its `"Unknown"` fallback. */
const ENTITY_LABELS: Readonly<Record<string, TranslationKey>> = {
  User: 'auditLog.entity.user',
  Content: 'auditLog.entity.content',
  Notification: 'auditLog.entity.notification',
  PlatformSetting: 'auditLog.entity.platformSetting',
  Role: 'auditLog.entity.role',
  Unknown: 'auditLog.entity.unknown',
};

/**
 * Labels for the most common fields across the ~11 auditable commands (`AuditBehavior.cs`), keyed
 * lowercase since the payload's property names arrive PascalCase from the C# JSON serializer and
 * are matched case-insensitively. Not exhaustive — a command with a field not listed here still
 * renders correctly via `humanize()`, just in English wording under an Arabic locale. Extending
 * coverage to every field of every current and future auditable command is unbounded scope; this
 * covers the fields an administrator is actually likely to read.
 */
const FIELD_LABELS: Readonly<Record<string, TranslationKey>> = {
  email: 'auditLog.field.email',
  username: 'auditLog.field.username',
  password: 'auditLog.field.password',
  firstname: 'auditLog.field.firstName',
  lastname: 'auditLog.field.lastName',
  phonenumber: 'auditLog.field.phoneNumber',
  roles: 'auditLog.field.roles',
  title: 'auditLog.field.title',
  body: 'auditLog.field.body',
  key: 'auditLog.field.key',
  value: 'auditLog.field.value',
  description: 'auditLog.field.description',
  category: 'auditLog.field.category',
  isactive: 'auditLog.field.isActive',
  ispublic: 'auditLog.field.isPublic',
  isencrypted: 'auditLog.field.isEncrypted',
  permissionids: 'auditLog.field.permissionIds',
  roleid: 'auditLog.field.roleId',
  categoryid: 'auditLog.field.categoryId',
  notificationtype: 'auditLog.field.notificationType',
  message: 'auditLog.field.message',
  userid: 'auditLog.field.userId',
  valuetype: 'auditLog.field.valueType',
};

/** One row of the parsed, human-readable recorded-payload list. */
export interface AuditDetailField {
  readonly label: string;
  readonly value: string;
}

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

  /** The action verb, localized — falls back to the raw server value if it's ever unrecognized. */
  actionLabel(action: string): string {
    const key = ACTION_LABELS[action];
    return key ? this.locale.t(key) : action;
  }

  /** The entity type noun, localized — falls back to the raw server value if unrecognized. */
  entityLabel(entityType: string): string {
    const key = ENTITY_LABELS[entityType];
    return key ? this.locale.t(key) : entityType;
  }

  /**
   * Turns the recorded payload's raw JSON into a labeled list instead of dumping the blob inline —
   * the actual UX gap this method exists to close. `json` is already redacted server-side
   * (`AuditBehavior.Redact`), so a `"***REDACTED***"` value here means the backend intentionally
   * hid it, not that this method needs to hide anything itself.
   */
  detailFields(json: string | null): readonly AuditDetailField[] {
    if (!json) {
      return [];
    }

    let parsed: unknown;
    try {
      parsed = JSON.parse(json);
    } catch {
      // Malformed/non-JSON payload is still a real record — show it rather than silently drop it.
      return [{ label: this.locale.t('auditLog.field.value'), value: json }];
    }

    if (typeof parsed !== 'object' || parsed === null) {
      return [{ label: this.locale.t('auditLog.field.value'), value: String(parsed) }];
    }

    return Object.entries(parsed as Record<string, unknown>).map(([key, value]) => ({
      label: this.fieldLabel(key),
      value: this.fieldValue(value),
    }));
  }

  private fieldLabel(key: string): string {
    const mapped = FIELD_LABELS[key.toLowerCase()];
    return mapped ? this.locale.t(mapped) : this.humanize(key);
  }

  /** PascalCase → spaced, capitalized words — the fallback for a field with no curated label. */
  private humanize(key: string): string {
    const spaced = key.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
    return spaced.charAt(0).toUpperCase() + spaced.slice(1);
  }

  private fieldValue(value: unknown): string {
    if (value === null || value === undefined || value === '') {
      return this.locale.t('auditLog.detail.empty');
    }
    if (value === '***REDACTED***') {
      return this.locale.t('auditLog.detail.redacted');
    }
    if (typeof value === 'boolean') {
      return this.locale.t(value ? 'auditLog.detail.yes' : 'auditLog.detail.no');
    }
    if (Array.isArray(value)) {
      return value.length
        ? value.map((item) => String(item)).join(', ')
        : this.locale.t('auditLog.detail.empty');
    }
    if (typeof value === 'object') {
      return JSON.stringify(value);
    }
    return String(value);
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
