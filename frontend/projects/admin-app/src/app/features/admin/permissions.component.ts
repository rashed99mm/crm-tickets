import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  ApiError,
  AsyncState,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsLoadingState,
  failed,
  loading,
  PermissionAdministration,
  PermissionAdministrationPermission,
  PermissionAdministrationRole,
  PermissionApi,
  TranslatePipe,
} from 'common';

@Component({
  selector: 'admin-permissions',
  imports: [CsCard, CsLoadingState, CsEmptyState, CsErrorState, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './permissions.component.html',
})
export default class PermissionsComponent {
  private readonly api = inject(PermissionApi);

  readonly state = signal<AsyncState<PermissionAdministration>>(loading());
  readonly mutating = signal<string | null>(null);
  readonly mutationError = signal<ApiError | null>(null);
  readonly mutationSuccess = signal<'assigned' | 'revoked' | null>(null);

  readonly data = computed(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : null;
  });

  readonly lastPermissionError = computed(() => this.mutationError()?.code === 'ERR002');

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  readonly summary = computed<{ roles: number; permissions: number } | null>(() => {
    const model = this.data();
    return model ? { roles: model.roles.length, permissions: model.permissions.length } : null;
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set(loading());
    this.api.list().subscribe({
      next: (result) => this.state.set(result.permissions.length ? { status: 'loaded', data: result } : { status: 'empty' }),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  toggle(role: PermissionAdministrationRole, permission: PermissionAdministrationPermission, checked: boolean): void {
    const key = `${role.id}:${permission.id}`;
    if (this.mutating()) return;
    this.mutating.set(key);
    this.mutationError.set(null);
    this.mutationSuccess.set(null);

    const request = checked
      ? this.api.assign(role.id, permission.id)
      : this.api.revoke(role.id, permission.id);
    request.subscribe({
      next: () => {
        this.mutating.set(null);
        this.mutationSuccess.set(checked ? 'assigned' : 'revoked');
        this.load();
      },
      error: (error: unknown) => {
        this.mutating.set(null);
        this.mutationError.set(this.toApiError(error));
      },
    });
  }

  isAssigned(role: PermissionAdministrationRole, permissionId: string): boolean {
    return role.permissionIds.includes(permissionId);
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
