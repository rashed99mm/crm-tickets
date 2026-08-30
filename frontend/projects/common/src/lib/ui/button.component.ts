import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

/**
 * The three variants Command Center documents, and only three (`AC-90`).
 *
 * Written as literal class strings rather than composed at runtime, for the
 * same reason the badge's are: Tailwind emits a rule only for a class it can
 * see in the source text.
 *
 * `secondary` states `bg-surface-lowest` explicitly instead of relying on the
 * page behind it — it sits on cards, on tinted strips and on the app canvas,
 * and "white" is what the design means, not "whatever is underneath".
 */
const VARIANTS = {
  primary:
    'bg-primary text-on-primary shadow-card hover:-translate-y-0.5 hover:shadow-popover active:translate-y-0 active:scale-[0.98]',
  secondary:
    'bg-surface-lowest border border-outline-variant text-on-surface shadow-sm hover:-translate-y-0.5 hover:bg-surface-bright hover:shadow-card active:translate-y-0 active:scale-[0.98]',
  ghost: 'text-primary hover:underline active:scale-[0.98]',
  danger:
    'bg-error text-on-error shadow-card hover:-translate-y-0.5 hover:shadow-popover active:translate-y-0 active:scale-[0.98]',
  icon:
    'grid size-9 place-items-center rounded-xl p-0 text-on-surface-variant hover:bg-surface-highest hover:text-on-surface active:scale-[0.96]',
} as const;

/**
 * The mockups show no disabled or loading button state; both are designed
 * here. `busy` disables as well as showing a spinner, because a double
 * submit on a slow connection creates two records — a real bug, and a
 * common one.
 */
@Component({
  selector: 'cs-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './button.component.html',
})
export class CsButton {
  readonly variant = input<keyof typeof VARIANTS>('primary');
  readonly type = input<'button' | 'submit'>('button');
  readonly disabled = input(false);
  readonly busy = input(false);
  readonly ariaLabel = input<string | null>(null);
  readonly pressed = output<void>();

  readonly tone = computed(() => VARIANTS[this.variant()]);
}
