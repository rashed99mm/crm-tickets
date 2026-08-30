import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../api/api-response';

/** Matches the backend's `AuditLogDto` — FEAT-21, AC-140. */
export interface AuditLogEntry {
  readonly id: string;
  readonly userId: string;
  readonly userName: string | null;
  readonly action: string;
  readonly entityType: string;
  readonly entityId: string;
  readonly oldValues: string | null;
  readonly newValues: string | null;
  readonly ipAddress: string | null;
  readonly userAgent: string | null;
  readonly createdAt: string;
}

export interface AuditLogFilters {
  readonly page?: number;
  readonly pageSize?: number;
  readonly actionType?: string;
  readonly userId?: string;
}

/** Audit trail reads. Admin only — the backend refuses anyone else with 403. */
@Injectable({ providedIn: 'root' })
export class AuditLogApi {
  private readonly http = inject(HttpClient);

  list(filters: AuditLogFilters = {}): Observable<PagedResult<AuditLogEntry>> {
    let params = new HttpParams()
      .set('page', String(filters.page ?? 1))
      .set('pageSize', String(filters.pageSize ?? 20));

    if (filters.actionType) {
      params = params.set('actionType', filters.actionType);
    }

    if (filters.userId) {
      params = params.set('userId', filters.userId);
    }

    return this.http.get<PagedResult<AuditLogEntry>>('/api/admin/audit-log', { params });
  }
}
