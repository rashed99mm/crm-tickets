import { ChangeDetectionStrategy, Component, effect, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { CsButton } from '../ui/button.component';
import { TranslatePipe } from '../i18n/translate.pipe';

/** Shared by all three report screens — US-610 AC1, narrowed to date range (spec addendum A4). */
@Component({
  selector: 'cs-report-date-range-filter',
  imports: [ReactiveFormsModule, CsButton, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './report-date-range-filter.component.html',
})
export class ReportDateRangeFilter {
  readonly from = input.required<string>();
  readonly to = input.required<string>();
  readonly apply = output<{ from: string; to: string }>();

  readonly form = new FormGroup({
    from: new FormControl('', { nonNullable: true }),
    to: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    // Keeps the form in step when the host navigates (e.g. back/forward changing the url's query
    // params) without fighting the user's own in-progress edits on first render.
    effect(() => {
      this.form.setValue({ from: this.from(), to: this.to() }, { emitEvent: false });
    });
  }

  submit(): void {
    const { from, to } = this.form.getRawValue();
    if (from && to) {
      this.apply.emit({ from, to });
    }
  }
}
