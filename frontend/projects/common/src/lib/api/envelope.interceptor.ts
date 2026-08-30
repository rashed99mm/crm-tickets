import { HttpErrorResponse, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { catchError, map, throwError } from 'rxjs';
import { ApiError } from './api-error';
import { ApiEnvelope, FieldError, isApiEnvelope } from './api-response';

const NETWORK_CODE = 'ERR_NETWORK';
const NETWORK_MESSAGE = 'Could not reach the server';

/**
 * FluentValidation reports `PropertyName` in PascalCase (`"Title"`); form controls are
 * camelCase (`title`). Lowercasing only the first character is deliberately narrow — it
 * is an assumption about every current validator (`F3` in the realignment spec), not a
 * general PascalCase-to-camelCase converter.
 */
function toControlName(propertyName: string): string {
  return propertyName.length === 0
    ? propertyName
    : propertyName.charAt(0).toLowerCase() + propertyName.slice(1);
}

/**
 * The ONLY place that knows the response envelope exists.
 *
 * Success bodies are unwrapped to `data`, so feature services return plain
 * typed models. Failures become a typed `ApiError`. A second place doing
 * this would let `success` and `errors` leak into components, which is what
 * FE-4 forbids — and it would mean two definitions of "what failure looks
 * like" drifting apart.
 */
export const envelopeInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    map((event) => {
      if (event instanceof HttpResponse && isApiEnvelope(event.body)) {
        const envelope = event.body as ApiEnvelope<unknown>;
        return event.clone({ body: envelope.data });
      }

      // Not enveloped — the OpenAPI document, static assets. Left untouched.
      return event;
    }),
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        // Something threw that was not an HTTP failure; do not disguise it.
        return throwError(() => error);
      }

      if (isApiEnvelope(error.error)) {
        const envelope = error.error as ApiEnvelope<unknown>;
        const fieldErrors: FieldError[] = (envelope.errors ?? []).map((e) => ({
          field: toControlName(e.field),
          code: e.code,
          message: e.message,
        }));

        return throwError(
          () =>
            new ApiError(
              envelope.code,
              envelope.message,
              fieldErrors,
              envelope.traceId ?? '',
              error.status,
            ),
        );
      }

      // A failure with no envelope: offline, a proxy error page, a 502 from
      // infrastructure that never reached the application. It still has to
      // arrive as something displayable rather than a raw ProgressEvent.
      return throwError(
        () => new ApiError(NETWORK_CODE, NETWORK_MESSAGE, [], '', error.status),
      );
    }),
  );
