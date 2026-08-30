import { TestBed } from '@angular/core/testing';
import { CsIcon } from './icon.component';

describe('CsIcon', () => {
  it('renders the Material Symbol ligature', () => {
    const fixture = TestBed.createComponent(CsIcon);
    fixture.componentRef.setInput('name', 'dashboard');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const span = el.querySelector('span')!;
    expect(span.textContent).toContain('dashboard');
  });

  it('has aria-hidden', () => {
    const fixture = TestBed.createComponent(CsIcon);
    fixture.componentRef.setInput('name', 'inbox');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('span')!.getAttribute('aria-hidden')).toBe('true');
  });

  it('applies filled class when filled is true', () => {
    const fixture = TestBed.createComponent(CsIcon);
    fixture.componentRef.setInput('name', 'person');
    fixture.componentRef.setInput('filled', true);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('span')!.className).toContain('is-filled');
  });
});
