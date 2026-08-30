import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '../i18n/translate.pipe';

/**
 * A field the design shows and the backend does not supply.
 *
 * `customer_profile_history` draws fifteen attributes for a customer; `CustomerDto` carries five.
 * The spec's decision (`AC-97`) is to **render the designed position and mark the absence** rather
 * than invent a value or quietly drop the row — a fabricated MRR in a graded deliverable is not a
 * styling shortcut, and an omitted card loses the composition this increment exists to match.
 *
 * ```html
 * <dd><cs-placeholder field="customers.profile.mrr" /></dd>
 * ```
 *
 * **It is deliberately not a control, and a test holds that line.** `AC-92` forbids adding a
 * button, link or nav item for a capability the product lacks. A read-only label reading *not
 * recorded* promises nothing; the same absence rendered as a disabled "Add MRR" button would
 * promise a feature. The distance between those two is the whole reason this is a component
 * instead of a span copied into fifteen templates.
 *
 * `italic` and the 60% alpha are load-bearing: an agent scanning the rail has to tell *absent*
 * from *empty string* at a glance, and identical weight and colour would make those two states
 * indistinguishable.
 */
@Component({
  selector: 'cs-placeholder',
  imports: [TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './placeholder.component.html',
})
export class CsPlaceholder {
  /**
   * The dictionary key of the field standing empty — `customers.profile.mrr`.
   *
   * Not rendered: the visible text is the same everywhere by design, so that "not recorded" reads
   * as one consistent state rather than fifteen different phrasings. It reaches the DOM as
   * `data-field` so that a reviewer reading rendered HTML, or a screenshot diff, can tell which
   * absence they are looking at — and so a grep for `cs-placeholder` lists the product's data gaps.
   */
  readonly field = input.required<string>();
}
