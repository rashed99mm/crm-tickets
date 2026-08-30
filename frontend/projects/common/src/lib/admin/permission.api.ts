import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface PermissionAdministrationPermission {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
}

export interface PermissionAdministrationRole {
  readonly id: string;
  readonly name: string;
  readonly permissionIds: readonly string[];
}

export interface PermissionAdministration {
  readonly roles: readonly PermissionAdministrationRole[];
  readonly permissions: readonly PermissionAdministrationPermission[];
}

@Injectable({ providedIn: 'root' })
export class PermissionApi {
  private readonly http = inject(HttpClient);

  list(): Observable<PermissionAdministration> {
    return this.http.get<PermissionAdministration>('/api/admin/permissions');
  }

  assign(roleId: string, permissionId: string): Observable<unknown> {
    return this.http.post(`/api/admin/permissions/${roleId}/${permissionId}`, {});
  }

  revoke(roleId: string, permissionId: string): Observable<unknown> {
    return this.http.delete(`/api/admin/permissions/${roleId}/${permissionId}`);
  }
}
