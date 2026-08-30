import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { ToastService } from './toast.service';

describe('ToastService', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({ providers: [ToastService] });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('prepends toast messages and dismisses them', () => {
    const service = TestBed.inject(ToastService);

    service.success('Saved');
    service.error('Failed', 'Try again');

    expect(service.items()[0]).toEqual(
      expect.objectContaining({ kind: 'error', title: 'Failed', message: 'Try again' }),
    );
    expect(service.items()[1]).toEqual(expect.objectContaining({ kind: 'success', title: 'Saved' }));

    service.dismiss(service.items()[0].id);

    expect(service.items()).toHaveLength(1);
    expect(service.items()[0].title).toBe('Saved');
  });

  it('auto-dismisses toast messages', () => {
    const service = TestBed.inject(ToastService);

    service.info('Queued');
    vi.advanceTimersByTime(5_000);

    expect(service.items()).toHaveLength(0);
  });
});
