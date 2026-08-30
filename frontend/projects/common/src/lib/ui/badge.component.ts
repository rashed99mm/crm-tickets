import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Status and priority, as the Command Center mockups draw them.
 *
 * Status is a **solid** chip; priority is a **tinted, outlined** chip carrying
 * a coloured dot. The difference is structural on purpose: the two sit side by
 * side in a queue row, and fill-versus-outline keeps them tellable apart in
 * greyscale and for colour-blind users, where hue alone would not.
 *
 * ---
 *
 * **Every class below is a literal string, and it has to stay that way.**
 * Tailwind builds its stylesheet by scanning source text for class names. A
 * class assembled at runtime — `` `bg-status-${value.toLowerCase()}` `` — never
 * appears in the source, so the scanner never emits the rule. The badge is then
 * styled in `ng serve` (where the JIT scanner has seen the neighbouring
 * literals) and **unstyled in the production build**: the worst failure mode
 * there is, because nothing fails, and the only symptom is a colourless queue
 * on a deployed server. Hence these `Record`s. Do not "simplify" them into
 * template literals.
 */

/**
 * Solid fills. The domain has New/Open/Assigned/In Progress/Waiting for Customer/
 * Waiting for Internal Team/Resolved/Closed; `escalated` is carried because the
 * token exists and S2 introduces the state — a badge that throws away a status
 * the server already sends is worse than one that shows it early.
 */
const STATUS_TONE: Readonly<Record<string, string>> = {
  new: 'bg-status-new text-on-primary',
  open: 'bg-status-open text-on-primary',
  assigned: 'bg-status-assigned text-on-primary',
  'in progress': 'bg-status-in-progress text-on-primary',
  'waiting for customer': 'bg-status-waiting-for-customer text-on-primary',
  'waiting for internal team': 'bg-status-waiting-for-internal-team text-on-primary',
  resolved: 'bg-status-resolved text-on-primary',
  closed: 'bg-status-closed text-on-primary',
  escalated: 'bg-status-escalated text-on-primary',
};

/** Tint + text + hairline, per priority. */
const PRIORITY_TONE: Readonly<Record<string, string>> = {
  low: 'bg-priority-low/10 text-priority-low border border-priority-low/20',
  normal: 'bg-priority-normal/10 text-priority-normal border border-priority-normal/20',
  high: 'bg-priority-high/10 text-priority-high border border-priority-high/20',
  urgent: 'bg-priority-urgent/10 text-priority-urgent border border-priority-urgent/20',
};

/** The dot in front of a priority. Separate map: a solid fill, not a tint. */
const PRIORITY_DOT: Readonly<Record<string, string>> = {
  low: 'bg-priority-low',
  normal: 'bg-priority-normal',
  high: 'bg-priority-high',
  urgent: 'bg-priority-urgent',
};

/**
 * An unrecognised value must render, not crash: the backend can add a state
 * before the frontend learns about it, and a queue that throws is a worse
 * outcome than a grey chip.
 */
const FALLBACK_TONE = 'bg-surface-highest text-on-surface-variant';
const FALLBACK_DOT = 'bg-on-surface-variant';

@Component({
  selector: 'cs-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './badge.component.html',
})
export class CsBadge {
  readonly kind = input.required<'status' | 'priority'>();

  /** The server-owned domain value: `Open`, `Urgent`. Never translated. */
  readonly value = input.required<string>();

  /**
   * Display text, when the caller has something better than the raw value.
   * Falls back to `value`. Domain identifiers stay untranslated — `MVP-13`
   * recorded why — so this is for disambiguation, not localisation.
   */
  readonly label = input<string>();

  readonly text = computed(() => this.label() ?? this.value());

  /** Matched case-insensitively: `Open` and `open` are the same state. */
  private readonly key = computed(() => this.value().toLowerCase());

  readonly tone = computed(() => {
    const map = this.kind() === 'status' ? STATUS_TONE : PRIORITY_TONE;

    return map[this.key()] ?? FALLBACK_TONE;
  });

  readonly dot = computed(() => PRIORITY_DOT[this.key()] ?? FALLBACK_DOT);
}
