import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CsIcon } from './icon.component';

/**
 * The empty state — a successful request that returned nothing.
 *
 * Deliberately has NO retry button. Nothing failed, so offering a retry
 * invites the user to "fix" a correct result. That absence is also the
 * visual difference that stops this reading as an error (AC-58), and it is
 * asserted by `AC58: a successful empty result renders the empty state, with
 * no retry offered`. **Do not add one.**
 *
 * The restyle widens that distance rather than narrowing it: a neutral
 * `inbox` glyph on a neutral surface, against the error state's red-tinted
 * panel, red `error` glyph and `role="alert"`.
 */
@Component({
  selector: 'cs-empty-state',
  imports: [CsIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './empty-state.component.html',
})
export class CsEmptyState {
  readonly message = input.required<string>();
  readonly hint = input<string | null>(null);
}
