import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../api/api-response';

/** Matches the backend's `UserListItemDto` (`FE-12`). No `displayName` — the backend
 * has no such field; the list is rendered from `firstName`/`lastName` directly. */
export interface StaffUser {
  readonly id: string;
  readonly email: string;
  readonly username: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly isActive: boolean;
  readonly createdAt: string;
  readonly roles: readonly string[];
  readonly departmentId?: string | null;
  readonly departmentName?: string | null;
}

/** Matches the backend's `CreateUserRequest` (`FE-13`). */
export interface CreateStaffRequest {
  readonly email: string;
  readonly username: string;
  readonly password: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly phoneNumber?: string;
  readonly roles?: readonly string[];
}

/** Matches the backend's `UserInfoDto` (`AC-430`, `AC-446`). */
export interface StaffProfile {
  readonly id: string;
  readonly email: string;
  readonly username: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly phoneNumber?: string | null;
  readonly emailConfirmed: boolean;
  readonly phoneNumberConfirmed?: boolean;
  readonly isActive: boolean;
  readonly createdAt: string;
  readonly roles: readonly string[];
  readonly profileImageUrl?: string | null;
}

/** Matches the backend's `UpdateCurrentUserProfileRequest` (`AC-430`, `AC-436`). */
export interface UpdateProfileRequest {
  readonly firstName: string;
  readonly lastName: string;
  readonly phoneNumber?: string | null;
  readonly profileImageUrl?: string | null;
}

/** Matches the backend's `VerifyOtpRequest` (`AC-439`, `AC-440`). The verification id returned by
 * the request endpoint is required — the backend refuses a verify without it (AC-443). */
export interface VerifyOtpRequest {
  readonly verificationId: string;
  readonly code: string;
}

/** Matches the backend's `RequestOtpResponse` (OTP-1/OTP-2/OTP-3). */
export interface OtpRequestResponse {
  readonly verificationId: string;
  readonly expiresAtUtc: string;
  readonly retryAfterSeconds: number;
  readonly channel: string;
}

export interface VerifyOtpResponse {
  readonly success?: boolean;
  readonly message?: string;
  readonly verified?: boolean;
}

/** Query parameters for the server-paged users list, one per `GET /api/Users` `[FromQuery]`. */
export interface UserListQuery {
  readonly page?: number;
  readonly pageSize?: number;
  /** Backend `SortBy` vocabulary: `email`, `username`, `firstname`, `lastname`, `createdat`, `lastlogin`. */
  readonly sortBy?: string | null;
  readonly sortDirection?: 'asc' | 'desc';
  readonly search?: string | null;
  readonly isActive?: boolean | null;
  readonly role?: string | null;
}

/**
 * Staff administration and self-service profile calls. Like AuthApi it catches nothing:
 * failures arrive as ApiError from the envelope interceptor.
 */
@Injectable({ providedIn: 'root' })
export class StaffApi {
  private readonly http = inject(HttpClient);

  list(query: UserListQuery = {}): Observable<PagedResult<StaffUser>> {
    const params: Record<string, string | number | boolean> = {
      page: query.page ?? 1,
      pageSize: query.pageSize ?? 10,
    };
    if (query.sortBy) params['sortBy'] = query.sortBy;
    if (query.sortDirection) params['sortDirection'] = query.sortDirection;
    if (query.search?.trim()) params['search'] = query.search.trim();
    if (query.isActive !== null && query.isActive !== undefined) params['isActive'] = query.isActive;
    if (query.role) params['role'] = query.role;
    return this.http.get<PagedResult<StaffUser>>('/api/Users', { params });
  }

  create(request: CreateStaffRequest): Observable<unknown> {
    return this.http.post('/api/Users', request);
  }

  setActive(id: string, isActive: boolean): Observable<unknown> {
    const action = isActive ? 'activate' : 'deactivate';
    return this.http.put(`/api/Users/${id}/${action}`, {});
  }

  changeOwnPassword(currentPassword: string, newPassword: string): Observable<unknown> {
    return this.http.post('/api/Auth/change-password', { currentPassword, newPassword });
  }

  getCurrentProfile(): Observable<StaffProfile> {
    return this.http.get<StaffProfile>('/api/Auth/me');
  }

  updateCurrentProfile(request: UpdateProfileRequest): Observable<StaffProfile> {
    return this.http.put<StaffProfile>('/api/Auth/me', request);
  }

  verifyOtp(request: VerifyOtpRequest): Observable<VerifyOtpResponse> {
    return this.http.post<VerifyOtpResponse>('/api/verification/verify', request);
  }

  requestPhoneVerification(phoneNumber: string): Observable<OtpRequestResponse> {
    return this.http.post<OtpRequestResponse>('/api/verification/request-phone', { phoneNumber });
  }
}

