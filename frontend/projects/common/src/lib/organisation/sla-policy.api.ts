import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../api/api-response';
import { TicketPriority } from '../tickets/ticket.api';

/** Matches the backend's `SLAPolicyDto` — FEAT-17, AC-124. */
export interface SLAPolicy {
  readonly id: string;
  readonly priority: TicketPriority;
  readonly responseTargetHours: number;
  readonly resolutionTargetHours: number;
  readonly categoryId: string | null;
  readonly branchId: string | null;
  readonly isActive: boolean;
  readonly createdAt: string;
}

/** The create/update payload — AC-119 (create), US-214 (update). */
export interface SLAPolicyRequest {
  readonly priority: TicketPriority;
  readonly responseTargetHours: number;
  readonly resolutionTargetHours: number;
  readonly categoryId?: string | null;
  readonly branchId?: string | null;
}

export interface BusinessHoursCalendar {
  readonly id: string;
  readonly branchId: string;
  readonly dayOfWeek: string;
  readonly openTime: string;
  readonly closeTime: string;
}

export interface BusinessHoursCalendarRequest {
  readonly branchId: string;
  readonly dayOfWeek: string;
  readonly openTime: string;
  readonly closeTime: string;
}

export interface PublicHoliday {
  readonly id: string;
  readonly branchId: string;
  readonly holidayDate: string;
  readonly name: string;
}

export interface PublicHolidayRequest {
  readonly branchId: string;
  readonly holidayDate: string;
  readonly name: string;
}

/**
 * SLA policy administration calls. Catches nothing: failures arrive as `ApiError` from the
 * envelope interceptor, the same rule every other API service in this workspace follows.
 */
@Injectable({ providedIn: 'root' })
export class SLAPolicyApi {
  private readonly http = inject(HttpClient);

  list(): Observable<PagedResult<SLAPolicy>> {
    return this.http.get<PagedResult<SLAPolicy>>('/api/SLAPolicies', {
      params: { pageSize: '100' },
    });
  }

  create(request: SLAPolicyRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/SLAPolicies', request);
  }

  update(id: string, request: SLAPolicyRequest): Observable<unknown> {
    return this.http.put(`/api/SLAPolicies/${id}`, request);
  }

  /** Soft-deactivates. */
  deactivate(id: string): Observable<unknown> {
    return this.http.delete(`/api/SLAPolicies/${id}`);
  }

  listBusinessHours(): Observable<PagedResult<BusinessHoursCalendar>> {
    return this.http.get<PagedResult<BusinessHoursCalendar>>('/api/BusinessHours/calendars', {
      params: { pageSize: '100' },
    });
  }

  createBusinessHours(request: BusinessHoursCalendarRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/BusinessHours/calendars', request);
  }

  listHolidays(): Observable<PagedResult<PublicHoliday>> {
    return this.http.get<PagedResult<PublicHoliday>>('/api/BusinessHours/holidays', {
      params: { pageSize: '100' },
    });
  }

  createHoliday(request: PublicHolidayRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/BusinessHours/holidays', request);
  }
}
