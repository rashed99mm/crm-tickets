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
 *
 * There is deliberately **no panel** — no border, no card, no ground. A
 * successful-but-empty result is the page having nothing to show, and a
 * bordered box around that sentence gives absence a physical presence it
 * should not have; at a glance it reads as a card that failed to load. What
 * is left is the glyph, the message, and a soft halo behind the glyph that is
 * `aria-hidden` decoration. The accessible content is exactly the message
 * and, when given, the hint.
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
