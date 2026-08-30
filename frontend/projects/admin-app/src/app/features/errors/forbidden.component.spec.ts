import { TestBed } from '@angular/core/testing';
import ForbiddenComponent from './forbidden.component';

describe('ForbiddenComponent', () => {
  it('renders the forbidden title', () => {
    const fixture = TestBed.createComponent(ForbiddenComponent);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const h2 = el.querySelector('h2');
    expect(h2).not.toBeNull();
  });
});
