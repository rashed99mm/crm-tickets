import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

/**
 * What the real sign-in endpoint returns, after the envelope interceptor has
 * unwrapped it (`FE-9`). Named after the backend's own `AuthResponse` record rather than
 * a locally invented shape, so there is nothing to translate at this boundary.
 * `accessTokenExpiresAt` / `refreshTokenExpiresAt` stay strings: ISO 8601 UTC on the wire
 * (`AC-54`) and nothing here does date arithmetic on them.
 */
export interface AuthResponse {
  readonly userId: string;
  readonly email: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly accessToken: string;
  readonly refreshToken: string;
  readonly accessTokenExpiresAt: string;
  readonly refreshTokenExpiresAt: string;
  readonly roles: readonly string[];
}

/**
 * What a portal visitor submits to create an account (`ASG-5`). Mirrors the backend
 * `RegisterRequest` record. `phoneNumber` is optional; the signup form sends `null` when blank,
 * never `""` (spec A4).
 */
export interface RegisterRequest {
  readonly email: string;
  readonly username: string;
  readonly password: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly phoneNumber: string | null;
}

/**
 * The only caller of the sign-in and refresh endpoints.
 *
 * It catches nothing. A rejection arrives as `ApiError` from the envelope
 * interceptor and the caller decides what to show - a service that
 * swallowed the error would have to invent a return value, which is how a
 * failed sign-in comes to look like a successful one.
 */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);

  signIn(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/Auth/login', { email, password });
  }

  /**
   * Creates an account via `POST /api/Auth/register`, which returns the new user's `Guid` — not
   * tokens (spec A2). The caller signs in separately to obtain a session.
   */
  register(payload: RegisterRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/Auth/register', payload);
  }

  /**
   * Exchanges an expired access token and a still-valid refresh token for a new pair.
   * Called by the single-flight refresh interceptor on a 401 (`FE-11`), never directly
   * by a feature.
   */
  refresh(accessToken: string, refreshToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/Auth/refresh', { accessToken, refreshToken });
  }
}
