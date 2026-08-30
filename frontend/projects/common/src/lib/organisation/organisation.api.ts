import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../api/api-response';

/** Matches the backend's `DepartmentDto` — FEAT-16, AC-115. */
export interface Department {
  readonly id: string;
  readonly name: string;
  readonly managerId: string | null;
  readonly isActive: boolean;
  readonly createdAt: string;
}

/** The create/update payload — AC-119. */
export interface DepartmentRequest {
  readonly name: string;
  readonly managerId?: string | null;
}

/**
 * Department administration calls. Catches nothing: failures arrive as `ApiError` from the
 * envelope interceptor, the same rule every other API service in this workspace follows.
 */
@Injectable({ providedIn: 'root' })
export class DepartmentApi {
  private readonly http = inject(HttpClient);

  list(): Observable<PagedResult<Department>> {
    return this.http.get<PagedResult<Department>>('/api/Departments', {
      params: { pageSize: '100' },
    });
  }

  create(request: DepartmentRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/Departments', request);
  }

  update(id: string, request: DepartmentRequest): Observable<unknown> {
    return this.http.put(`/api/Departments/${id}`, request);
  }

  /** Soft-deactivates — AC-119. */
  deactivate(id: string): Observable<unknown> {
    return this.http.delete(`/api/Departments/${id}`);
  }
}
