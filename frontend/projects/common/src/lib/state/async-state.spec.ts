import { ApiError } from '../api/api-error';
import { empty, failed, fromList, idle, loaded, loading } from './async-state';

describe('AsyncState', () => {
  it('distinguishes every state', () => {
    expect(idle().status).toBe('idle');
    expect(loading().status).toBe('loading');
    expect(loaded(['a']).status).toBe('loaded');
    expect(empty().status).toBe('empty');
  });

  it('carries data on loaded and the error on failure', () => {
    const state = loaded({ id: 1 });
    expect(state.status === 'loaded' && state.data).toEqual({ id: 1 });

    const error = new ApiError(
      'ERR010',
      'Not found',
      [],
      '00-a',
      404,
    );

    const failedState = failed(error);
    expect(failedState.status === 'error' && failedState.error.code).toBe('ERR010');
  });

  it('maps an empty list to empty and a populated one to loaded', () => {
    expect(fromList([]).status).toBe('empty');
    expect(fromList(['a']).status).toBe('loaded');
  });

  it('never represents a failure as empty', () => {
    // FE-9. The bug this exists to prevent is catchError(() => of([])), which
    // turns a 500 into "no results" — the user then reports missing data and
    // nobody goes looking for the real fault.
    const error = new ApiError(
      'ERR900',
      'Server error',
      [],
      '00-b',
      500,
    );

    const state = failed<readonly string[]>(error);

    expect(state.status).toBe('error');
    expect(state.status).not.toBe('empty');
  });
});
