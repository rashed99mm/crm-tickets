import { TestBed } from '@angular/core/testing';
import { CsButton } from './button.component';

function button(variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'icon'): HTMLButtonElement {
  const fixture = TestBed.createComponent(CsButton);
  if (variant) {
    fixture.componentRef.setInput('variant', variant);
  }
  fixture.detectChanges();
  return (fixture.nativeElement as HTMLElement).querySelector('button')!;
}

describe('CsButton', () => {
  it('AC90: a primary button is filled with the brand colour', () => {
    expect(button('primary').className).toContain('bg-primary');
    expect(button('primary').className).toContain('text-on-primary');
  });

  it('AC90: a secondary button is not filled', () => {
    // The design's secondary is white with a hairline: it must not carry the
    // primary fill, or the two read as the same control and the hierarchy the
    // three variants exist to express is gone.
    const secondary = button('secondary').className;

    expect(secondary).not.toContain('bg-primary');
    expect(secondary).toContain('bg-surface-lowest');
    expect(secondary).toContain('border-outline-variant');
  });

  it('AC90: a ghost button has no border and no background', () => {
    const ghost = button('ghost').className;

    expect(ghost).not.toContain('border');
    expect(ghost).not.toContain('bg-');
    expect(ghost).toContain('text-primary');
  });

  it('AC90: primary is the default variant', () => {
    expect(button().className).toContain('bg-primary');
  });

  it('AC500: a danger button is visually distinct from the primary action', () => {
    const danger = button('danger').className;

    expect(danger).toContain('bg-error');
    expect(danger).toContain('text-on-error');
  });

  it('AC500: an icon button has stable square dimensions', () => {
    const icon = button('icon').className;

    expect(icon).toContain('size-9');
    expect(icon).toContain('place-items-center');
  });

  it('AC500: an icon-only button can expose an accessible name', () => {
    const fixture = TestBed.createComponent(CsButton);
    fixture.componentRef.setInput('variant', 'icon');
    fixture.componentRef.setInput('ariaLabel', 'Refresh');
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('button')?.getAttribute('aria-label')).toBe(
      'Refresh',
    );
  });

  it('busy disables the button as well as showing a spinner', () => {
    // Behaviour, not styling: a double submit on a slow connection creates two
    // records. Restyling must not lose this.
    const fixture = TestBed.createComponent(CsButton);
    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('button')!.disabled).toBe(true);
    expect(el.querySelector('.animate-spin')).not.toBeNull();
  });
});
