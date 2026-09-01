import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Observable, catchError, concat, map, of, toArray } from 'rxjs';
import {
  ApiError,
  AsyncState,
  ConfirmationService,
  CsButton,
  CsCard,
  CsIcon,
  CsEmptyState,
  CsErrorState,
  CsLoadingState,
  failed,
  loading,
  PermissionAdministration,
  PermissionAdministrationPermission,
  PermissionAdministrationRole,
  PermissionApi,
  ToastService,
  LocaleStore,
  TranslationKey,
  TranslatePipe,
} from 'common';

/** One staged difference between the loaded snapshot and the draft. */
export interface PermissionChange {
  readonly roleId: string;
  readonly roleName: string;
  readonly permissionId: string;
  readonly permissionName: string;
  readonly kind: 'grant' | 'revoke';
}

/** One role whose atomic save was refused, with the server's own reason. */
export interface RoleSaveFailure {
  readonly roleId: string;
  readonly roleName: string;
  readonly code: string;
  readonly message: string;
}

/** The result of one save across n dirty roles. Atomic per role, so partial outcomes are real. */
export interface SaveOutcome {
  readonly saved: number;
  readonly total: number;
  readonly failures: readonly RoleSaveFailure[];
}

/** The permission ids each role would hold if the draft were saved. */
type Draft = ReadonlyMap<string, ReadonlySet<string>>;

/** A permission column, or the single placeholder column a collapsed group leaves behind. */
export type MatrixColumn =
  | { readonly kind: 'permission'; readonly groupKey: string; readonly permission: PermissionAdministrationPermission }
  | { readonly kind: 'summary'; readonly groupKey: string; readonly count: number };

interface PermissionGroup {
  readonly key: string;
  readonly label: string;
  readonly permissions: readonly PermissionAdministrationPermission[];
  readonly collapsed: boolean;
}

/**
 * Group keys come from the permission names themselves (spec A1), so this map only supplies the
 * human label. `TranslationKey` keeps it honest: a typo is a compile error, and a group with no
 * entry falls back to its raw key rather than rendering a blank header.
 */
const GROUP_LABELS: Readonly<Record<string, TranslationKey>> = {
  ticket: 'permissions.group.ticket',
  customer: 'permissions.group.customer',
  report: 'permissions.group.report',
  user: 'permissions.group.user',
  other: 'permissions.group.other',
};

/** What the unsaved-changes route guard needs from this screen. */
export interface UnsavedChangesHost {
  hasUnsavedChanges(): boolean;
  confirmLeave(): Observable<boolean>;
}

/**
 * US-806 — the role permission workbench.
 *
 * The screen this replaces sent a `POST` or `DELETE` on every checkbox click, with no confirmation
 * and no undo: re-scoping a role meant eight requests and eight intermediate states that were each
 * briefly live. Here a click mutates a local draft, `changes()` diffs that draft against the loaded
 * snapshot, and saving sends one atomic `PUT` per dirty role after the administrator has seen the
 * list of what will change.
 *
 * Checked state still follows the server, never the click (spec A7): a successful save reloads.
 */
@Component({
  selector: 'admin-permissions',
  imports: [CsCard, CsButton, CsIcon, CsLoadingState, CsEmptyState, CsErrorState, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './permissions.component.html',
  host: { '(window:beforeunload)': 'onBeforeUnload($event)' },
})
export default class PermissionsComponent implements UnsavedChangesHost {
  private readonly api = inject(PermissionApi);
  private readonly toast = inject(ToastService);
  private readonly locale = inject(LocaleStore);
  private readonly confirmations = inject(ConfirmationService);

  readonly state = signal<AsyncState<PermissionAdministration>>(loading());
  readonly draft = signal<Draft>(new Map());
  readonly saving = signal(false);
  readonly saveOutcome = signal<SaveOutcome | null>(null);
  readonly search = signal('');
  readonly collapsedGroups = signal<ReadonlySet<string>>(new Set());

  readonly data = computed(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : null;
  });

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  readonly summary = computed<{ roles: number; permissions: number } | null>(() => {
    const model = this.data();
    return model ? { roles: model.roles.length, permissions: model.permissions.length } : null;
  });

  /**
   * The staged diff. Grants first, then revokes, per role in load order — a stable order matters
   * because this list is what the confirmation dialog shows (AC-806.12).
   */
  readonly changes = computed<readonly PermissionChange[]>(() => {
    const model = this.data();
    const draft = this.draft();
    if (!model) {
      return [];
    }

    const names = new Map(model.permissions.map((permission) => [permission.id, permission.name]));
    const changes: PermissionChange[] = [];

    for (const role of model.roles) {
      const stored = new Set(role.permissionIds);
      const desired = draft.get(role.id) ?? stored;

      for (const permissionId of desired) {
        if (!stored.has(permissionId)) {
          changes.push({
            roleId: role.id,
            roleName: role.name,
            permissionId,
            permissionName: names.get(permissionId) ?? permissionId,
            kind: 'grant',
          });
        }
      }
      for (const permissionId of stored) {
        if (!desired.has(permissionId)) {
          changes.push({
            roleId: role.id,
            roleName: role.name,
            permissionId,
            permissionName: names.get(permissionId) ?? permissionId,
            kind: 'revoke',
          });
        }
      }
    }

    return changes;
  });

  readonly isDirty = computed(() => this.changes().length > 0);

  readonly dirtyRoleIds = computed<readonly string[]>(() => [
    ...new Set(this.changes().map((change) => change.roleId)),
  ]);

  /** Roles refused with ERR087 — the only failure a reload can resolve. */
  readonly staleRoleIds = computed<readonly string[]>(() =>
    this.saveOutcome()?.failures.filter((failure) => failure.code === 'ERR087').map((failure) => failure.roleId) ?? [],
  );

  readonly groups = computed<readonly PermissionGroup[]>(() => {
    const model = this.data();
    if (!model) {
      return [];
    }

    const term = this.search().trim().toLowerCase();
    const collapsed = this.collapsedGroups();
    const buckets = new Map<string, PermissionAdministrationPermission[]>();

    for (const permission of model.permissions) {
      const haystack = `${permission.name} ${permission.description ?? ''}`.toLowerCase();
      if (term && !haystack.includes(term)) {
        continue;
      }

      const separator = permission.name.indexOf('.');
      const key = separator > 0 ? permission.name.slice(0, separator) : 'other';
      const bucket = buckets.get(key);
      if (bucket) {
        bucket.push(permission);
      } else {
        buckets.set(key, [permission]);
      }
    }

    return [...buckets].map(([key, permissions]) => ({
      key,
      label: GROUP_LABELS[key] ? this.locale.t(GROUP_LABELS[key]) : key,
      permissions,
      collapsed: collapsed.has(key),
    }));
  });

  /** Header row 2 and every body row iterate this, so the two stay aligned by construction. */
  readonly columns = computed<readonly MatrixColumn[]>(() =>
    this.groups().flatMap((group): MatrixColumn[] =>
      group.collapsed
        ? [{ kind: 'summary', groupKey: group.key, count: group.permissions.length }]
        : group.permissions.map((permission) => ({
            kind: 'permission',
            groupKey: group.key,
            permission,
          })),
    ),
  );

  /** The permissions a bulk action or a save-visible-count applies to (AC-806.23). */
  readonly visiblePermissions = computed<readonly PermissionAdministrationPermission[]>(() =>
    this.groups().filter((group) => !group.collapsed).flatMap((group) => group.permissions),
  );

  readonly hasNoMatch = computed(() => this.search().trim().length > 0 && this.groups().length === 0);

  /**
   * What a screen reader hears when the staged count changes. Rendered in a live region that exists
   * for the life of the screen (AC-806.25) — a region created at the same moment as its text is not
   * announced, which is why this returns the clean sentence rather than an empty string.
   */
  readonly announcement = computed(() => {
    const count = this.changes().length;
    return count === 0
      ? this.locale.t('permissions.announceClean')
      : this.locale.t('permissions.pending', count);
  });

  constructor() {
    this.load();
  }

  /**
   * Reloads the matrix and reseeds the draft from it. `retain` re-overlays the desired sets of roles
   * whose save was refused, so a rejected role keeps the administrator's intent while every other
   * role shows server truth (AC-806.15).
   */
  load(retain: Draft = new Map()): void {
    this.state.set(loading());
    this.api.list().subscribe({
      next: (result) => {
        this.state.set(
          result.permissions.length ? { status: 'loaded', data: result } : { status: 'empty' },
        );
        this.draft.set(this.seedDraft(result, retain));
      },
      error: (error: unknown) => {
        const apiError = this.toApiError(error);
        this.state.set(failed(apiError));
        this.draft.set(new Map());
        this.toast.error(this.locale.t('error.generic.title'), this.locale.t('permissions.loadError'));
      },
    });
  }

  /** AC-806.20 — a reload throws staged work away, so it asks when there is any. */
  refresh(): void {
    if (!this.isDirty()) {
      this.load();
      return;
    }

    this.confirmations
      .confirm({
        title: this.locale.t('permissions.refreshConfirm.title', this.changes().length),
        message: this.locale.t('permissions.leaveConfirm.body'),
        confirmText: this.locale.t('action.refresh'),
        cancelText: this.locale.t('action.cancel'),
        danger: true,
      })
      .subscribe((accepted) => {
        if (accepted) {
          this.saveOutcome.set(null);
          this.load();
        }
      });
  }

  isChecked(roleId: string, permissionId: string): boolean {
    return this.draft().get(roleId)?.has(permissionId) ?? false;
  }

  /** True when the draft and the loaded snapshot disagree about this cell. */
  isStaged(roleId: string, permissionId: string): boolean {
    const role = this.data()?.roles.find((candidate) => candidate.id === roleId);
    if (!role) {
      return false;
    }
    return role.permissionIds.includes(permissionId) !== this.isChecked(roleId, permissionId);
  }

  /** The direction of a staged cell, for its visible marker. `null` when the cell is unchanged. */
  stagedDirection(roleId: string, permissionId: string): 'grant' | 'revoke' | null {
    if (!this.isStaged(roleId, permissionId)) {
      return null;
    }
    return this.isChecked(roleId, permissionId) ? 'grant' : 'revoke';
  }

  toggle(
    role: PermissionAdministrationRole,
    permission: PermissionAdministrationPermission,
    checked: boolean,
  ): void {
    if (this.saving()) {
      return;
    }

    this.draft.update((draft) => {
      const next = new Map(draft);
      const desired = new Set(next.get(role.id) ?? role.permissionIds);
      if (checked) {
        desired.add(permission.id);
      } else {
        desired.delete(permission.id);
      }
      next.set(role.id, desired);
      return next;
    });
  }

  /** AC-806.23 — stages every currently visible permission for this role. Nothing is sent. */
  grantAll(role: PermissionAdministrationRole): void {
    this.stageBulk(role, true);
  }

  revokeAll(role: PermissionAdministrationRole): void {
    this.stageBulk(role, false);
  }

  toggleGroup(key: string): void {
    this.collapsedGroups.update((collapsed) => {
      const next = new Set(collapsed);
      if (!next.delete(key)) {
        next.add(key);
      }
      return next;
    });
  }

  clearSearch(): void {
    this.search.set('');
  }

  /** `assigned / total` for one role within one group, read from the draft (AC-806.22). */
  groupCount(roleId: string, groupKey: string): string {
    const group = this.groups().find((candidate) => candidate.key === groupKey);
    if (!group) {
      return '';
    }
    const assigned = group.permissions.filter((permission) => this.isChecked(roleId, permission.id)).length;
    return `${assigned}/${group.permissions.length}`;
  }

  columnKey(column: MatrixColumn): string {
    return column.kind === 'permission' ? `permission:${column.permission.id}` : `summary:${column.groupKey}`;
  }

  /** AC-806.12 — the dialog is the gate; nothing is sent from here. */
  save(): void {
    const changes = this.changes();
    if (!changes.length || this.saving()) {
      return;
    }

    this.confirmations
      .confirm({
        title: this.locale.t('permissions.saveConfirm.title', changes.length),
        message: this.locale.t('permissions.saveConfirm.body'),
        details: changes.map((change) => this.describe(change)),
        confirmText: this.locale.t('permissions.saveConfirm.confirm'),
        cancelText: this.locale.t('action.cancel'),
        danger: changes.some((change) => change.kind === 'revoke'),
      })
      .subscribe((accepted) => {
        if (accepted) {
          this.apply();
        }
      });
  }

  /** AC-806.18 — local reset, confirmed. Never re-reads from the server. */
  discard(): void {
    const model = this.data();
    if (!model || !this.isDirty()) {
      return;
    }

    this.confirmations
      .confirm({
        title: this.locale.t('permissions.discardConfirm.title', this.changes().length),
        message: this.locale.t('permissions.discardConfirm.body'),
        confirmText: this.locale.t('permissions.discardConfirm.confirm'),
        cancelText: this.locale.t('action.cancel'),
        danger: true,
      })
      .subscribe((accepted) => {
        if (!accepted) {
          return;
        }
        this.saveOutcome.set(null);
        this.draft.set(this.seedDraft(model, new Map()));
      });
  }

  describe(change: PermissionChange): string {
    return this.locale.t(
      change.kind === 'grant' ? 'permissions.change.grant' : 'permissions.change.revoke',
      change.permissionName,
      change.roleName,
    );
  }

  /** Read by the route guard (`permissionsDirtyGuard`) and by the `beforeunload` backstop below. */
  hasUnsavedChanges(): boolean {
    return this.isDirty();
  }

  /**
   * AC-806.19 — asked by `permissionsDirtyGuard`. Returns the dialog's answer directly: `true`
   * leaves, `false` stays with the draft intact.
   */
  confirmLeave(): Observable<boolean> {
    return this.confirmations.confirm({
      title: this.locale.t('permissions.leaveConfirm.title', this.changes().length),
      message: this.locale.t('permissions.leaveConfirm.body'),
      confirmText: this.locale.t('permissions.leaveConfirm.confirm'),
      cancelText: this.locale.t('action.cancel'),
      danger: true,
    });
  }

  onBeforeUnload(event: BeforeUnloadEvent): void {
    // The browser shows its own untranslatable prompt here; it is a backstop for a closed tab, not
    // a designed screen (spec A9). In-app navigation uses the styled dialog instead.
    if (this.hasUnsavedChanges()) {
      event.preventDefault();
    }
  }

  /** AC-806.16 — drops only the stale roles' drafts; other refused roles keep theirs. */
  reloadStale(): void {
    const stale = new Set(this.staleRoleIds());
    const retain = this.retainOf(
      this.dirtyRoleIds().filter((roleId) => !stale.has(roleId)),
      this.draft(),
    );
    this.saveOutcome.set(null);
    this.load(retain);
  }

  private stageBulk(role: PermissionAdministrationRole, granted: boolean): void {
    if (this.saving()) {
      return;
    }

    const visible = this.visiblePermissions();
    this.draft.update((draft) => {
      const next = new Map(draft);
      const desired = new Set(next.get(role.id) ?? role.permissionIds);
      for (const permission of visible) {
        if (granted) {
          desired.add(permission.id);
        } else {
          desired.delete(permission.id);
        }
      }
      next.set(role.id, desired);
      return next;
    });
  }

  /**
   * One `PUT` per dirty role, sequentially. `concat` rather than `forkJoin`: the backend takes a
   * per-role lock, so parallelism buys nothing and would make a partial outcome depend on
   * interleaving.
   */
  private apply(): void {
    const model = this.data();
    if (!model) {
      return;
    }

    const draft = this.draft();
    const dirty = this.dirtyRoleIds();
    const roles = model.roles.filter((role) => dirty.includes(role.id));

    this.saving.set(true);
    this.saveOutcome.set(null);

    const requests = roles.map((role) =>
      this.api
        .setRolePermissions(role.id, [...(draft.get(role.id) ?? [])], [...role.permissionIds])
        .pipe(
          map(() => ({ role, error: null as ApiError | null })),
          catchError((error: unknown) => of({ role, error: this.toApiError(error) })),
        ),
    );

    concat(...requests)
      .pipe(toArray())
      .subscribe((results) => {
        this.saving.set(false);
        const failures = results
          .filter((result) => result.error !== null)
          .map<RoleSaveFailure>((result) => ({
            roleId: result.role.id,
            roleName: result.role.name,
            code: result.error!.code,
            message: this.failureMessage(result.error!),
          }));

        if (!failures.length) {
          this.saveOutcome.set(null);
          this.toast.success(this.locale.t('permissions.saveSuccess'));
          this.load();
          return;
        }

        this.saveOutcome.set({
          saved: results.length - failures.length,
          total: results.length,
          failures,
        });
        this.toast.error(
          this.locale.t('error.generic.title'),
          this.locale.t('permissions.savePartial', results.length - failures.length, results.length),
        );

        // Reload so every role that DID save shows server truth (spec A7), then re-overlay the
        // refused roles' intent so nothing the administrator asked for is silently discarded.
        this.load(this.retainOf(failures.map((failure) => failure.roleId), draft));
      });
  }

  /**
   * The server's refusal, in the administrator's language. `ERR002` and `ERR087` are the two the
   * screen can explain; anything else gets the generic message rather than a raw server string,
   * which may be an unlocalised internal detail.
   */
  private failureMessage(error: ApiError): string {
    switch (error.code) {
      case 'ERR002':
        return this.locale.t('permissions.lastRequired');
      case 'ERR087':
        return this.locale.t('permissions.staleRole');
      default:
        return this.locale.t('permissions.mutationError');
    }
  }

  private seedDraft(model: PermissionAdministration, retain: Draft): Draft {
    const draft = new Map<string, ReadonlySet<string>>();
    for (const role of model.roles) {
      draft.set(role.id, new Set(retain.get(role.id) ?? role.permissionIds));
    }
    return draft;
  }

  private retainOf(roleIds: readonly string[], draft: Draft): Draft {
    const retained = new Map<string, ReadonlySet<string>>();
    for (const roleId of roleIds) {
      const desired = draft.get(roleId);
      if (desired) {
        retained.set(roleId, desired);
      }
    }
    return retained;
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError
      ? error
      : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
