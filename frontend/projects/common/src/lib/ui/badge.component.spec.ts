import { TestBed } from '@angular/core/testing';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { CsBadge } from './badge.component';

function render(kind: 'status' | 'priority', value: string, label?: string): HTMLElement {
  const fixture = TestBed.createComponent(CsBadge);
  fixture.componentRef.setInput('kind', kind);
  fixture.componentRef.setInput('value', value);
  if (label !== undefined) {
    fixture.componentRef.setInput('label', label);
  }
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

/** The badge chip itself — the outer span, not the dot inside it. */
function chip(kind: 'status' | 'priority', value: string): HTMLElement {
  return render(kind, value).querySelector('span')!;
}

describe('CsBadge', () => {
  // The shape assertions from the pre-restyle suite ("a pill for priority, a
  // box for status") were replaced deliberately: the Command Center design
  // gives both the same 4px radius and carries the distinction as fill versus
  // outline-plus-dot instead. That distinction is asserted below, so the
  // property those tests protected — the two stay tellable apart without
  // colour — is still covered.

  it('AC89: a status badge carries its status colour class', () => {
    // Named literally, one per domain status, because the class string is the
    // thing under test: this is what proves the queue is colour-coded rather
    // than eight grey chips.
    expect(chip('status', 'New').className).toContain('bg-status-new');
    expect(chip('status', 'Open').className).toContain('bg-status-open');
    expect(chip('status', 'Assigned').className).toContain('bg-status-assigned');
    expect(chip('status', 'In Progress').className).toContain('bg-status-in-progress');
    expect(chip('status', 'Waiting for Customer').className).toContain('bg-status-waiting-for-customer');
    expect(chip('status', 'Waiting for Internal Team').className).toContain('bg-status-waiting-for-internal-team');
    expect(chip('status', 'Resolved').className).toContain('bg-status-resolved');
    expect(chip('status', 'Closed').className).toContain('bg-status-closed');
  });

  it('AC89: every domain status is distinguishable from every other', () => {
    const classes = ['New', 'Open', 'Assigned', 'In Progress', 'Waiting for Customer', 'Waiting for Internal Team', 'Resolved', 'Closed'].map(
      (status) => chip('status', status).className,
    );

    expect(new Set(classes).size).toBe(8);
  });

  it('AC89: a priority badge carries the dot and the tinted classes', () => {
    for (const [priority, token] of [
      ['Low', 'priority-low'],
      ['Normal', 'priority-normal'],
      ['High', 'priority-high'],
      ['Urgent', 'priority-urgent'],
    ] as const) {
      const badge = render('priority', priority);
      const outer = badge.querySelector('span')!;

      // Tinted background, coloured text, hairline border — not a solid fill.
      expect(outer.className).toContain(`bg-${token}/10`);
      expect(outer.className).toContain(`text-${token}`);
      expect(outer.className).toContain(`border-${token}/20`);

      // The mockup's dot indicator, in the solid colour.
      const dot = outer.querySelector('span')!;
      expect(dot.className).toContain('rounded-full');
      expect(dot.className).toContain(`bg-${token}`);
    }
  });

  it('AC89: status is filled and priority is outlined, so the two differ without colour', () => {
    const status = chip('status', 'Open');
    const priority = chip('priority', 'Urgent');

    // Solid fill, no dot.
    expect(status.className).toContain('text-on-primary');
    expect(status.querySelector('span')).toBeNull();

    // Outlined, with a dot.
    expect(priority.className).toContain('border');
    expect(priority.querySelector('span')).not.toBeNull();
  });

  it('AC89: badge classes are literals, not built by concatenation', () => {
    // Tailwind scans SOURCE TEXT for class names. A class assembled at runtime
    // is invisible to that scan, so the rule is never emitted and the badge
    // renders unstyled in the production build while looking fine in dev.
    //
    // So this test checks BOTH ends: the class reaches the DOM, *and* it
    // appears verbatim in the component's source where the scanner can find
    // it. Rewriting the Records as template literals keeps every other test in
    // this file green and fails only this one — which is the entire point.
    const source = readFileSync(
      join(process.cwd(), 'projects/common/src/lib/ui/badge.component.ts'),
      'utf8',
    );

    const rendered = [
      chip('status', 'New').className,
      chip('status', 'Open').className,
      chip('status', 'Assigned').className,
      chip('status', 'In Progress').className,
      chip('status', 'Waiting for Customer').className,
      chip('status', 'Waiting for Internal Team').className,
      chip('status', 'Resolved').className,
      chip('status', 'Closed').className,
      chip('priority', 'Low').className,
      chip('priority', 'Normal').className,
      chip('priority', 'High').className,
      chip('priority', 'Urgent').className,
    ].join(' ');

    for (const literal of [
      'bg-status-new',
      'bg-status-open',
      'bg-status-assigned',
      'bg-status-in-progress',
      'bg-status-waiting-for-customer',
      'bg-status-waiting-for-internal-team',
      'bg-status-resolved',
      'bg-status-closed',
      'bg-priority-low/10',
      'bg-priority-normal/10',
      'bg-priority-high/10',
      'bg-priority-urgent/10',
    ]) {
      expect(rendered).toContain(literal);
      expect(source).toContain(literal);
    }
  });

  it('renders an unknown value without throwing', () => {
    // The backend may send a state the frontend has not learned about. A grey
    // chip is a far better outcome than a queue that throws.
    expect(() => render('status', 'Hibernating')).not.toThrow();
    expect(chip('status', 'Hibernating').className).toContain('bg-surface-highest');
  });

  it('matches the value case-insensitively', () => {
    expect(chip('status', 'open').className).toContain('bg-status-open');
  });

  it('shows the label when given one, and the raw value otherwise', () => {
    expect(render('status', 'Open', 'Open now').textContent).toContain('Open now');
    expect(render('status', 'Open').textContent).toContain('Open');
  });
});
