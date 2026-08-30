import { TestBed } from '@angular/core/testing';
import { CsLoadingState } from './loading-state.component';

describe('CsLoadingState', () => {
  it('renders with default text', () => {
    const fixture = TestBed.createComponent(CsLoadingState);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="status"]')).not.toBeNull();
  });

  it('renders a spinner', () => {
    const fixture = TestBed.createComponent(CsLoadingState);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.animate-spin')).not.toBeNull();
  });
});
