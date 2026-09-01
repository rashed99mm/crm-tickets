import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConfirmationService } from './confirmation.service';
import { CsConfirmationHost } from './confirmation-host.component';

describe('CsConfirmationHost', () => {
  let fixture: ComponentFixture<CsConfirmationHost>;
  let confirmations: ConfirmationService;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [ConfirmationService] });
    confirmations = TestBed.inject(ConfirmationService);
    fixture = TestBed.createComponent(CsConfirmationHost);
    fixture.detectChanges();
  });

  function host(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  async function open(request: Parameters<ConfirmationService['confirm']>[0]) {
    const answered: (boolean | null)[] = [null];
    confirmations.confirm(request).subscribe((accepted) => (answered[0] = accepted));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return answered;
  }

  it('AC807_2_EscapeCancelsTheDialog: closes and resolves false', async () => {
    const answered = await open({ title: 'Delete', message: 'Delete this?', danger: true });
    expect(host().querySelector('[role="alertdialog"]')).not.toBeNull();

    const backdrop = host().querySelector('div')!;
    backdrop.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(answered[0]).toBe(false);
    expect(host().querySelector('[role="alertdialog"]')).toBeNull();
  });

  it('AC807_2_OtherKeysDoNotCloseTheDialog: only Escape dismisses', async () => {
    const answered = await open({ title: 'Delete', message: 'Delete this?' });

    const backdrop = host().querySelector('div')!;
    backdrop.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();

    expect(answered[0]).toBeNull();
    expect(host().querySelector('[role="alertdialog"]')).not.toBeNull();
  });

  it('AC807_3_FocusMovesToCancelForADangerRequest', async () => {
    await open({ title: 'Delete', message: 'Delete this?', danger: true });

    const cancel = host().querySelector<HTMLButtonElement>('[data-testid="confirm-cancel"]')!;
    expect(document.activeElement).toBe(cancel);
  });

  it('AC807_3_FocusReturnsToTheTriggerOnClose', async () => {
    // A real trigger the user pressed before the dialog opened.
    const trigger = document.createElement('button');
    trigger.textContent = 'Deactivate';
    document.body.appendChild(trigger);
    trigger.focus();
    expect(document.activeElement).toBe(trigger);

    try {
      await open({ title: 'Delete', message: 'Delete this?', danger: true });
      expect(document.activeElement).not.toBe(trigger);

      confirmations.resolve(false);
      fixture.detectChanges();
      await fixture.whenStable();
      await fixture.whenStable();

      expect(document.activeElement).toBe(trigger);
    } finally {
      trigger.remove();
    }
  });

  it('AC807_4_RendersDetailsAsAList', async () => {
    await open({
      title: 'Apply changes',
      message: '2 changes',
      details: ['Grant ticket.close → Agent', 'Revoke report.export → Supervisor'],
    });

    const items = host().querySelectorAll('[data-testid="confirm-details"] li');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('Grant ticket.close → Agent');
    expect(items[1].textContent).toContain('Revoke report.export → Supervisor');
  });

  it('AC807_4_OmitsTheListWhenThereAreNoDetails', async () => {
    await open({ title: 'Delete', message: 'Delete this?' });

    expect(host().querySelector('[data-testid="confirm-details"]')).toBeNull();
  });

  it('AC807_1_ShowsTheNextQueuedRequestAfterOneResolves', async () => {
    await open({ title: 'First', message: 'First?' });
    confirmations.confirm({ title: 'Second', message: 'Second?' }).subscribe();
    fixture.detectChanges();

    confirmations.resolve(true);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(host().textContent).toContain('Second');
    expect(host().textContent).not.toContain('First?');
  });
});
