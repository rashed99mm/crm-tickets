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
    class:
      'group flex flex-col overflow-hidden rounded-2xl border border-outline-variant/70 bg-surface-lowest shadow-card transition-[box-shadow,transform,border-color] duration-200 hover:-translate-y-0.5 hover:border-primary/20 hover:shadow-[0_14px_35px_rgba(11,28,48,0.08)]',
  },
  templateUrl: './card.component.html',
})
export class CsCard {
  /** Already localised by the caller. Omitted means no header strip. */
  readonly heading = input<string>();
}
