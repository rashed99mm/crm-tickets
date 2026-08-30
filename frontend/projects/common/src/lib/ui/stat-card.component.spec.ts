import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CsStatCard, StatDelta } from './stat-card.component';

describe('CsStatCard', () => {
  function render(overrides: Partial<{ icon: string; label: string; value: string | number; iconTone: string; delta: StatDelta; href: string }> = {}) {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(CsStatCard);
    fixture.componentRef.setInput('icon', overrides.icon ?? 'inbox');
    fixture.componentRef.setInput('label', overrides.label ?? 'Open Tickets');
    fixture.componentRef.setInput('value', overrides.value ?? '42');
    if (overrides.iconTone !== undefined) fixture.componentRef.setInput('iconTone', overrides.iconTone);
    if (overrides.delta !== undefined) fixture.componentRef.setInput('delta', overrides.delta);
    if (overrides.href !== undefined) fixture.componentRef.setInput('href', overrides.href);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the icon, label and value', () => {
    const el = render({ icon: 'inbox', label: 'Open Tickets', value: '42' });
    expect(el.querySelector('cs-icon')).not.toBeNull();
    expect(el.textContent).toContain('Open Tickets');
    expect(el.textContent).toContain('42');
  });

  it('shows a delta with up direction and good tone', () => {
    const el = render({ delta: { value: '12%', direction: 'up', tone: 'good' } });
    const badge = el.querySelector('span.inline-flex')!;
    expect(badge.className).toContain('text-success');
    expect(el.textContent).toContain('12%');
  });

  it('shows a delta with bad tone', () => {
    const el = render({ delta: { value: '5%', direction: 'down', tone: 'bad' } });
    expect(el.querySelector('span.inline-flex')!.className).toContain('text-error');
  });

  it('becomes a link when href is set', () => {
    const el = render({ href: '/tickets' });
    expect(el.querySelector('a')).not.toBeNull();
  });

  it('does not render a link when href is absent', () => {
    const el = render();
    expect(el.querySelector('a')).toBeNull();
  });
});
