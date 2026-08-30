import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * A status as the mockup *tables* draw it: a tinted, outlined pill carrying a
 * solid dot in the status colour.
 *
 * This is distinct from `cs-badge kind="status"`, which is a **solid** fill used
 * in headers and dense chips. The two render the same domain state differently on
 * purpose: a solid badge in a header, a tinted-with-dot pill in a table row where
 * it sits beside the priority pill (also tinted-with-dot). Keeping both tinted in
 * the row preserves the fill-versus-tint tell that `cs-badge` established.
 *
 * Class strings are literals (see `cs-badge` for why) so Tailwind emits them.
 */
const STATUS_TINT: Readonly<Record<string, string>> = {
  new: 'bg-status-new/10 text-status-new border border-status-new/20',
  open: 'bg-status-open/10 text-status-open border border-status-open/20',
  assigned: 'bg-status-assigned/10 text-status-assigned border border-status-assigned/20',
  'in progress': 'bg-status-in-progress/10 text-status-in-progress border border-status-in-progress/20',
  'waiting for customer': 'bg-status-waiting-for-customer/10 text-status-waiting-for-customer border border-status-waiting-for-customer/20',
  'waiting for internal team': 'bg-status-waiting-for-internal-team/10 text-status-waiting-for-internal-team border border-status-waiting-for-internal-team/20',
  resolved: 'bg-status-resolved/10 text-status-resolved border border-status-resolved/20',
  closed: 'bg-status-closed/10 text-status-closed border border-status-closed/20',
  escalated: 'bg-status-escalated/10 text-status-escalated border border-status-escalated/20',
};

const STATUS_DOT: Readonly<Record<string, string>> = {
  new: 'bg-status-new',
  open: 'bg-status-open',
  assigned: 'bg-status-assigned',
  'in progress': 'bg-status-in-progress',
  'waiting for customer': 'bg-status-waiting-for-customer',
  'waiting for internal team': 'bg-status-waiting-for-internal-team',
  resolved: 'bg-status-resolved',
  closed: 'bg-status-closed',
  escalated: 'bg-status-escalated',
};

const FALLBACK_TINT = 'bg-surface-highest/40 text-on-surface-variant border border-border-subtle';
const FALLBACK_DOT = 'bg-on-surface-variant';

@Component({
  selector: 'cs-status-pill',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './status-pill.component.html',
})
export class CsStatusPill {
  /** Server-owned domain value: `Open`. Never translated. */
  readonly value = input.required<string>();

  /** Display text; falls back to `value`. */
  readonly label = input<string>();

  readonly text = computed(() => this.label() ?? this.value());

  private readonly key = computed(() => this.value().toLowerCase());

  readonly tone = computed(() => STATUS_TINT[this.key()] ?? FALLBACK_TINT);
  readonly dot = computed(() => STATUS_DOT[this.key()] ?? FALLBACK_DOT);
}
