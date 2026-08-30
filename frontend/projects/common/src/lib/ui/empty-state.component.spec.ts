import { TestBed } from '@angular/core/testing';
import { CsEmptyState } from './empty-state.component';

describe('CsEmptyState', () => {
  it('renders the message', () => {
    const fixture = TestBed.createComponent(CsEmptyState);
    fixture.componentRef.setInput('message', 'No tickets');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('No tickets');
  });

  it('renders the hint when provided', () => {
    const fixture = TestBed.createComponent(CsEmptyState);
    fixture.componentRef.setInput('message', 'Empty');
    fixture.componentRef.setInput('hint', 'Try creating one');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Try creating one');
  });

  it('does not render a hint when not provided', () => {
    const fixture = TestBed.createComponent(CsEmptyState);
    fixture.componentRef.setInput('message', 'Empty');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('p').length).toBe(1);
  });
});
