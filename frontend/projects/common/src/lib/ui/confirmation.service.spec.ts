import { TestBed } from '@angular/core/testing';
import { ConfirmationService } from './confirmation.service';

describe('ConfirmationService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [ConfirmationService] });
  });

  it('exposes a pending request and resolves the caller observable', () => {
    const service = TestBed.inject(ConfirmationService);
    let result: boolean | null = null;

    service
      .confirm({ title: 'Delete item', message: 'Delete this item?', danger: true })
      .subscribe((accepted) => {
        result = accepted;
      });

    expect(service.current()?.title).toBe('Delete item');

    service.resolve(true);

    expect(result).toBe(true);
    expect(service.current()).toBeNull();
  });

  it('AC807_1_QueuesASecondRequestInsteadOfDroppingIt: both callers receive a result', () => {
    const service = TestBed.inject(ConfirmationService);
    const results: (boolean | null)[] = [null, null];
    const completed = [false, false];

    service.confirm({ title: 'First', message: 'First?' }).subscribe({
      next: (accepted) => (results[0] = accepted),
      complete: () => (completed[0] = true),
    });
    service.confirm({ title: 'Second', message: 'Second?' }).subscribe({
      next: (accepted) => (results[1] = accepted),
      complete: () => (completed[1] = true),
    });

    // The first request stays on screen — a dialog the user is reading is never replaced.
    expect(service.current()?.title).toBe('First');
    expect(service.pendingCount()).toBe(2);

    service.resolve(true);

    expect(results[0]).toBe(true);
    expect(completed[0]).toBe(true);
    // The second is now current, and crucially has NOT been resolved yet.
    expect(service.current()?.title).toBe('Second');
    expect(results[1]).toBeNull();

    service.resolve(false);

    expect(results[1]).toBe(false);
    expect(completed[1]).toBe(true);
    expect(service.current()).toBeNull();
    expect(service.pendingCount()).toBe(0);
  });

  it('AC807_1_ResolveWithAnEmptyQueueIsANoOp: does not throw', () => {
    const service = TestBed.inject(ConfirmationService);

    expect(() => service.resolve(true)).not.toThrow();
    expect(service.current()).toBeNull();
  });

  it('AC807_1_ARequestQueuedFromAResolveCallbackLandsBehindTheQueue: no reentrancy loss', () => {
    const service = TestBed.inject(ConfirmationService);
    const seen: string[] = [];

    // A caller that opens a follow-up dialog the moment its own resolves — exactly what the
    // permissions screen does when a save failure prompts a reload.
    service.confirm({ title: 'First', message: 'First?' }).subscribe(() => {
      seen.push('first');
      service.confirm({ title: 'Third', message: 'Third?' }).subscribe(() => seen.push('third'));
    });
    service.confirm({ title: 'Second', message: 'Second?' }).subscribe(() => seen.push('second'));

    service.resolve(true);
    expect(service.current()?.title).toBe('Second');

    service.resolve(true);
    expect(service.current()?.title).toBe('Third');

    service.resolve(true);
    expect(seen).toEqual(['first', 'second', 'third']);
    expect(service.current()).toBeNull();
  });

  it('AC807_4_CarriesDetails: details travel with the request', () => {
    const service = TestBed.inject(ConfirmationService);

    service
      .confirm({
        title: 'Apply changes',
        message: '2 changes',
        details: ['Grant ticket.close → Agent', 'Revoke report.export → Supervisor'],
      })
      .subscribe();

    expect(service.current()?.details).toEqual([
      'Grant ticket.close → Agent',
      'Revoke report.export → Supervisor',
    ]);
  });
});
