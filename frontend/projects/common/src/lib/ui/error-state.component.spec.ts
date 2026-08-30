import { TestBed } from '@angular/core/testing';
import { CsErrorState } from './error-state.component';

describe('CsErrorState', () => {
  it('renders with role="alert"', () => {
    const fixture = TestBed.createComponent(CsErrorState);
    fixture.componentRef.setInput('error', {
      code: 'ERR_TEST',
      message_: { ar: 'خطأ', en: 'Error' },
      fieldErrors: [],
      raw: '',
      status: 500,
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')).not.toBeNull();
  });
});
