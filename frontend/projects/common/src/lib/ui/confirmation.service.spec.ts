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
});
