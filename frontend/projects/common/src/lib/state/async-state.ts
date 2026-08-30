import { ApiError } from '../api/api-error';

/**
 * Async state as a closed union, so a template is forced to handle every case.
 *
 * `empty` and `error` are separate members on purpose. Modelling async work
 * as "data or nothing" is what makes `catchError(() => of([]))` look
 * reasonable — and that turns a server failure into "no results". The user
 * then reports that their tickets are missing, and nobody looks for the real
 * fault because the UI said there was nothing to show.
 */
export type AsyncState<T> =
  | { readonly status: 'idle' }
  | { readonly status: 'loading' }
  | { readonly status: 'loaded'; readonly data: T }
  | { readonly status: 'empty' }
  | { readonly status: 'error'; readonly error: ApiError };

export const idle = <T>(): AsyncState<T> => ({ status: 'idle' });

export const loading = <T>(): AsyncState<T> => ({ status: 'loading' });

export const loaded = <T>(data: T): AsyncState<T> => ({ status: 'loaded', data });

export const empty = <T>(): AsyncState<T> => ({ status: 'empty' });

export const failed = <T>(error: ApiError): AsyncState<T> => ({
  status: 'error',
  error,
});

/**
 * Collapses a successfully fetched list into loaded-or-empty.
 *
 * Only ever call this on a SUCCESS path. Passing it a fallback array from a
 * failed request is precisely the mistake this union exists to prevent.
 */
export function fromList<T>(items: readonly T[]): AsyncState<readonly T[]> {
  return items.length === 0 ? empty() : loaded(items);
}
