import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../api/api-response';

/** Matches the backend's `PlatformSettingDto`. */
export interface PlatformSetting {
  readonly id: string;
  readonly key: string;
  readonly value: string;
  readonly description: string | null;
  readonly category: string;
  readonly valueType: string;
  readonly isEncrypted: boolean;
  readonly isPublic: boolean;
  readonly createdAt: string;
}

/** The update payload — only what a settings screen ever changes. */
export interface UpdatePlatformSettingRequest {
  readonly value?: string;
  readonly description?: string | null;
  readonly isEncrypted?: boolean;
  readonly isPublic?: boolean;
}

/**
 * Platform settings calls (US-803). Admin sees every setting; the backend's own `IncludePrivate`
 * gate (keyed to the caller's role) narrows a non-admin's view — this client sends no such flag,
 * it only ever runs behind the Admin-gated route.
 */
@Injectable({ providedIn: 'root' })
export class PlatformSettingApi {
  private readonly http = inject(HttpClient);

  list(): Observable<PagedResult<PlatformSetting>> {
    return this.http.get<PagedResult<PlatformSetting>>('/api/PlatformSettings', {
      params: { pageSize: '100' },
    });
  }

  update(id: string, request: UpdatePlatformSettingRequest): Observable<unknown> {
    return this.http.put(`/api/PlatformSettings/id/${encodeURIComponent(id)}`, request);
  }
}
