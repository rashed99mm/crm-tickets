import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SlaCountdown } from './sla-countdown.component';

describe('SlaCountdown', () => {
  function render(dueAt: string | null, createdAt: string): ComponentFixture<SlaCountdown> {
    const fixture = TestBed.createComponent(SlaCountdown);
    fixture.componentRef.setInput('dueAt', dueAt);
    fixture.componentRef.setInput('createdAt', createdAt);
    fixture.componentRef.setInput('label', 'Response due');
    fixture.detectChanges();
    return fixture;
  }

  it('AC155: renders nothing when there is no due date', () => {
    const fixture = render(null, '2026-08-27T00:00:00Z');
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');
  });

  it('AC156: renders the danger style once the due date has passed', () => {
    const past = new Date(Date.now() - 60_000).toISOString();
    const created = new Date(Date.now() - 3_600_000).toISOString();
    const fixture = render(past, created);

    const el = (fixture.nativeElement as HTMLElement).querySelector('[data-urgency]');
    expect(el?.getAttribute('data-urgency')).toBe('danger');
  });

  it('AC156: renders the warning style once under 20% of the window remains', () => {
    const created = new Date(Date.now() - 100_000).toISOString();
    // Total window ~110s (created 100s ago, due in 10s more) — remaining 10s is < 20% of 110s.
    const due = new Date(Date.now() + 10_000).toISOString();
    const fixture = render(due, created);

    const el = (fixture.nativeElement as HTMLElement).querySelector('[data-urgency]');
    expect(el?.getAttribute('data-urgency')).toBe('warning');
  });

  it('AC156: renders the normal style with most of the window remaining', () => {
    const created = new Date(Date.now() - 10_000).toISOString();
    const due = new Date(Date.now() + 990_000).toISOString();
    const fixture = render(due, created);

    const el = (fixture.nativeElement as HTMLElement).querySelector('[data-urgency]');
    expect(el?.getAttribute('data-urgency')).toBe('normal');
  });
});
