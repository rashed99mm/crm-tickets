import { FieldError } from './api-response';

/**
 * A failure that arrived as a well-formed envelope, or a transport failure
 * normalised into the same shape so call sites never branch on which kind
 * of failure they got.
 *
 * `message_` is a plain string, not `LocalizedMessage`, because the backend
 * resolves one language per response. `Error.message` already owns the
 * `message` property as a string, so the underscore suffix avoids shadowing.
 */
export class ApiError extends Error {
  constructor(
    readonly code: string,
    readonly message_: string,
    readonly errors: readonly FieldError[],
    readonly traceId: string,
    readonly status: number,
  ) {
    // Error.message stays a plain string for readable stack traces.
    super(`${code}: ${message_}`);
    this.name = 'ApiError';
  }

  /**
   * The error for one form control, by the field name the server used.
   * This is what binds a server rejection to the input that caused it.
   */
  fieldError(field: string): FieldError | undefined {
    return this.errors.find((error) => error.field === field);
  }

  get hasFieldErrors(): boolean {
    return this.errors.length > 0;
  }
}
