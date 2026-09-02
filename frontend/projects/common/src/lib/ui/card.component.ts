import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The Command Center card — the single most repeated shape in the mockups.
 * Every panel, table, form and metric group sits inside one.
 *
 * ```html
 * <cs-card [heading]="'tickets.title' | t">
 *   <button action …>…</button>
 *   <div class="p-4">…</div>
 * </cs-card>
 * ```
 *
 * Omit `heading` and the header strip is not rendered at all — a bare surface,
 * which is what the mockups' metric tiles are.
 *
 * **The body is deliberately unpadded.** The mockups' table rows run flush to
 * the card edge and supply their own `px-4`; a default padding here would
 * double it, and there is no way for the card to tell a table from a form.
 * Content that wants breathing room wraps itself.
 *
 * `heading` is a plain string because the caller has already resolved it
 * through the dictionary (`'x.title' | t`). The card never translates.
 */
@Component({
  selector: 'cs-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    // A layered, low-opacity shadow rather than one soft blur, and no hover lift: five of these
    // stacked in a column made the whole page twitch on mouse-over, which reads cheap. The card
    // now settles — only the border and shadow depth respond.
    class:
      'group flex flex-col overflow-hidden rounded-2xl border border-outline-variant/50 bg-surface-lowest shadow-[0_1px_2px_rgba(11,28,48,0.04),0_10px_28px_-14px_rgba(11,28,48,0.10)] transition-[box-shadow,border-color] duration-300 hover:border-primary/25 hover:shadow-[0_1px_2px_rgba(11,28,48,0.05),0_18px_44px_-18px_rgba(11,28,48,0.16)]',
  },
  templateUrl: './card.component.html',
})
export class CsCard {
  /** Already localised by the caller. Omitted means no header strip. */
  readonly heading = input<string>();
}
