import { TestBed } from '@angular/core/testing';
import { CsCard } from './card.component';

describe('CsCard', () => {
  it('renders without a heading', () => {
    const fixture = TestBed.createComponent(CsCard);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('h2')).toBeNull();
  });

  it('renders a heading when one is provided', () => {
    const fixture = TestBed.createComponent(CsCard);
    fixture.componentRef.setInput('heading', 'Tickets');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const h2 = el.querySelector('h2');
    expect(h2).not.toBeNull();
    expect(h2!.textContent).toContain('Tickets');
  });
});
