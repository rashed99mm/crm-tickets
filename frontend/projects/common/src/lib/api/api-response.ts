/**
 * The wire contract with the backend.
 *
 * The CCE Platform's `Response<T>` shape: `{ success, code, message, data, errors[], traceId, timestamp }`.
 * Only the envelope interceptor should reference `ApiEnvelope`: everything downstream sees
 * unwrapped data or an `ApiError`.
 */

/**
 * Bilingual string pair for client-owned text (translations, locale store).
 * Server messages arrive as a plain string — the backend resolves one language per response.
 */
export interface LocalizedMessage {
  readonly ar: string;
  readonly en: string;
}

/**
 * One field-level validation failure from the backend.
 * `field` arrives as PascalCase (FluentValidation `PropertyName`), and the envelope
 * interceptor lowercases the first character to match Angular form control names.
 * `message` is a plain string — the backend resolves one language per response.
 */
export interface FieldError {
  readonly field: string;
  readonly code: string;
  readonly message: string;
}

/**
 * The backend's `Response<T>` envelope. Success responses carry `data`; failure
 * responses carry `errors[]`. The envelope interceptor unwraps success to `data`
 * and failure to an `ApiError` — no component or feature service should read
 * these fields directly.
 */
export interface ApiEnvelope<T> {
  readonly success: boolean;
  readonly code: string;
  readonly message: string;
  readonly data: T | null;
  readonly errors: readonly FieldError[];
  readonly traceId?: string;
  readonly timestamp?: string;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly pageIndex: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

/**
 * Distinguishes an enveloped response from one that is not — the OpenAPI
 * document and static assets come back bare and must pass through untouched.
 */
export function isApiEnvelope(body: unknown): body is ApiEnvelope<unknown> {
  return (
    typeof body === 'object' &&
    body !== null &&
    'success' in body &&
    'data' in body &&
    'code' in body
  );
}
