import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * A Material Symbols Outlined glyph.
 *
 * The font is loaded from Google Fonts by each app's `index.html` (assumption
 * `A21`) and the `font-variation-settings` incantation lives in `theme.css`.
 * This component exists so that neither detail is repeated in fifteen
 * templates: `<cs-icon name="dashboard" />`, `<cs-icon name="warning" filled />`.
 *
 * **`aria-hidden` is unconditional, and that is a decision, not an oversight.**
 * A Material Symbol renders by ligature, so its text content is the literal
 * word `dashboard` — a screen reader with the font unloaded would read it out.
 * Every icon in this design sits beside its own visible label, so announcing
 * the ligature would either duplicate that label or read an English word into
 * an Arabic page. An icon that ever needs to be announced needs a label on its
 * container, not an exception here.
 *
 * The name is a font ligature, not translatable text: `no-hardcoded-strings`
 * has nothing to catch here, and there is nothing for a translator to change.
 */
@Component({
  selector: 'cs-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './icon.component.html',
})
export class CsIcon {
  /** The Material Symbols ligature — `dashboard`, `inbox`, `logout`. */
  readonly name = input.required<string>();

  /** Filled variant, for the active nav item and for warnings. */
  readonly filled = input(false);

  /** Optical size in px. 20 matches `label-lg`; the mockups' chrome uses 24. */
  readonly size = input(20);
}
