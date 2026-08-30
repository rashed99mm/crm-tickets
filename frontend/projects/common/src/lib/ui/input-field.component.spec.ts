import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FormControl, Validators } from '@angular/forms';
import { FieldError } from '../api/api-response';
import { CsInputField } from './input-field.component';

function render(control: FormControl, serverError: FieldError | null = null) {
  const fixture = TestBed.createComponent(CsInputField);
  fixture.componentRef.setInput('label', 'Email');
  fixture.componentRef.setInput('control', control);
  fixture.componentRef.setInput('serverError', serverError);
  fixture.detectChanges();
  return fixture;
}

describe('CsInputField', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  it('shows no error while untouched and pristine', () => {
    // AC-59: a form the user has not filled in must not be a wall of red.
    const el = render(new FormControl('', Validators.required))
      .nativeElement as HTMLElement;

    expect(el.querySelector('[role="alert"]')).toBeNull();
  });

  it('shows the error once touched', () => {
    const control = new FormControl('', Validators.required);
    control.markAsTouched();

    const el = render(control).nativeElement as HTMLElement;

    expect(el.querySelector('[role="alert"]')).not.toBeNull();
  });

  it('shows a server field error even on an untouched control', () => {
    // AC-60: the server already rejected this. Hiding the reason until the
    // user pokes the field is worse than useless.
    const el = render(new FormControl('x'), {
      field: 'email',
      code: 'VAL003',
      message: 'Invalid email',
    }).nativeElement as HTMLElement;

    expect(el.querySelector('[role="alert"]')!.textContent).toContain(
      'Invalid email',
    );
  });

  it('links the error to the input with aria-describedby and aria-invalid', () => {
    const control = new FormControl('', Validators.required);
    control.markAsTouched();

    const el = render(control).nativeElement as HTMLElement;
    const input = el.querySelector('input')!;
    const describedBy = input.getAttribute('aria-describedby');

    expect(describedBy).toBeTruthy();
    expect(el.querySelector(`#${describedBy}`)).not.toBeNull();
    expect(input.getAttribute('aria-invalid')).toBe('true');
  });

  it('associates the label with the input', () => {
    // A placeholder is not a label.
    const el = render(new FormControl('')).nativeElement as HTMLElement;
    const input = el.querySelector('input')!;

    expect(el.querySelector(`label[for="${input.id}"]`)).not.toBeNull();
  });
});
