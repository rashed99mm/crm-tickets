import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ApiError } from '../api/api-error';
import { CsEmptyState } from './empty-state.component';
import { CsErrorState } from './error-state.component';
import { CsLoadingState } from './loading-state.component';

@Component({
  selector: 'cs-chart-frame',
  imports: [CsEmptyState, CsErrorState, CsLoadingState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './chart-frame.component.html',
})
export class CsChartFrame {
  readonly title = input.required<string>();
  readonly loading = input(false);
  readonly empty = input(false);
  readonly emptyMessage = input.required<string>();
  readonly error = input<ApiError | null>(null);
}
