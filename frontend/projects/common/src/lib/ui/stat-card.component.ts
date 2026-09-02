import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgTemplateOutlet } from '@angular/common';
import { CsIcon } from './icon.component';
import { CsCard } from './card.component';

/**
 * The mockups' metric tile — `user_dashboard` / `agent_dashboard_overview`.
 *
 * An icon chip top-left, an optional delta top-right, an uppercase `label-md`
 * caption, and the number at `display` size. Built once here so the dashboard
 * (and any future report screen) shares one tile instead of each inventing a
 * subtly different box.
 *
 * `iconTone` and `delta` are optional; a bare tile (icon + value + label) is the
 * common case. All strings are passed already-localised by the caller — the card
 * never translates, matching `CsCard`.
 */
export interface StatDelta {
  readonly value: string;
  readonly direction: 'up' | 'down';
  readonly tone: 'good' | 'bad' | 'neutral';
}

@Component({
  selector: 'cs-stat-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CsIcon, CsCard, RouterLink, NgTemplateOutlet],
  templateUrl: './stat-card.component.html',
})
export class CsStatCard {
  readonly icon = input.required<string>();
  readonly label = input.required<string>();
  readonly value = input.required<string | number>();

  /** Extra classes for the icon chip, e.g. `bg-error-container/30 text-on-error-container`. */
  readonly iconTone = input('bg-surface-high text-primary');

  /**
   * Optional line under the value, saying what the number counts — "waiting for owner",
   * "across 4 agents". Report screens need it (a bare `12` under "UNASSIGNED" is ambiguous);
   * the dashboard's tiles omit it, which is why it is optional rather than required.
   */
  readonly hint = input<string | null>(null);

  /** Optional trend badge, top-right. */
  readonly delta = input<StatDelta>();

  /**
   * Renders a skeleton where the value goes, for a tile whose figure is still in flight.
   *
   * Report screens were each hand-rolling this, which is how four tiles on one screen ended up
   * with four different boxes. It matters that it exists at all: a pending figure and a figure
   * that came back empty are different facts, and showing an em dash for both says the data
   * arrived and was nothing.
   */
  readonly loading = input(false);

  /** When set, the whole card becomes a link. */
  readonly href = input<string>();
  readonly queryParams = input<Record<string, string | number | boolean>>();
}
