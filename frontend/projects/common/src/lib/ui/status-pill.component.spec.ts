import { TestBed } from '@angular/core/testing';
import { CsStatusPill } from './status-pill.component';

describe('CsStatusPill', () => {
  function render(value: string, label?: string): HTMLElement {
    const fixture = TestBed.createComponent(CsStatusPill);
    fixture.componentRef.setInput('value', value);
    if (label !== undefined) fixture.componentRef.setInput('label', label);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function pill(value: string): HTMLElement {
    return render(value).querySelector('span')!;
  }

  it('carries the status colour as a tinted, outlined pill with a solid dot', () => {
    const el = render('Open');
    const outer = el.querySelector('span')!;
    expect(outer.className).toContain('bg-status-open/10');
    expect(outer.className).toContain('text-status-open');
    expect(outer.className).toContain('border-status-open/20');
    const dot = outer.querySelector('span')!;
    expect(dot.className).toContain('rounded-full');
    expect(dot.className).toContain('bg-status-open');
  });

  it('maps every domain status to a distinct tint', () => {
    const classes = ['New', 'Open', 'Assigned', 'In Progress', 'Waiting for Customer', 'Waiting for Internal Team', 'Resolved', 'Closed'].map(
      (s) => pill(s).className,
    );
    expect(new Set(classes).size).toBe(8);
  });

  it('falls back without throwing on an unknown status', () => {
    expect(() => render('Hibernating')).not.toThrow();
    expect(pill('Hibernating').className).toContain('bg-surface-highest');
  });

  it('shows the label when given one, else the raw value', () => {
    expect(render('Open', 'Open now').textContent).toContain('Open now');
    expect(render('Open').textContent).toContain('Open');
  });

  it('matches the value case-insensitively', () => {
    expect(pill('open').className).toContain('bg-status-open');
  });

  it('AC403_StatusAndPriorityRemainSemanticInBothPalettes: status and priority pills remain distinct and semantic', () => {
    const statuses = ['New', 'Open', 'Assigned', 'In Progress', 'Waiting for Customer', 'Waiting for Internal Team', 'Resolved', 'Closed', 'Escalated'];
    for (const st of statuses) {
      const p = pill(st);
      expect(p.className).toContain(`bg-status-${st.toLowerCase().replace(/\s+/g, '-')}`);
      expect(p.className).toContain(`text-status-${st.toLowerCase().replace(/\s+/g, '-')}`);
    }
  });
});

