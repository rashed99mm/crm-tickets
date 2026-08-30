import { TestBed } from '@angular/core/testing';
import { CsSlaPill } from './sla-pill.component';

describe('CsSlaPill', () => {
  it('AC501: renders SLA warning state distinctly', () => {
    const fixture = TestBed.createComponent(CsSlaPill);
    fixture.componentRef.setInput('state', 'warning');
    fixture.detectChanges();

    const pill = (fixture.nativeElement as HTMLElement).querySelector('span')!;
    expect(pill.className).toContain('bg-warning/10');
    expect(pill.textContent).toContain('At risk');
  });

  it('AC501: renders unavailable when no SLA summary exists', () => {
    const fixture = TestBed.createComponent(CsSlaPill);
    fixture.detectChanges();

    const pill = (fixture.nativeElement as HTMLElement).querySelector('span')!;
    expect(pill.className).toContain('bg-surface-highest');
    expect(pill.textContent).toContain('No SLA');
  });
});
