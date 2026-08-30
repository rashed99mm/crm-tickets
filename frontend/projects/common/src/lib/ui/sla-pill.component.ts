import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CsIcon } from './icon.component';

export type SlaVisualState = 'healthy' | 'warning' | 'breached' | 'paused' | 'unavailable';

const SLA_TONE: Readonly<Record<SlaVisualState, string>> = {
  healthy: 'border-success/20 bg-success/10 text-success',
  warning: 'border-warning/20 bg-warning/10 text-warning',
  breached: 'border-error/20 bg-error/10 text-error',
  paused: 'border-outline-variant bg-surface-highest text-on-surface-variant',
  unavailable: 'border-border-subtle bg-surface-highest/60 text-on-surface-variant',
};

const SLA_ICON: Readonly<Record<SlaVisualState, string>> = {
  healthy: 'check_circle',
  warning: 'timer',
  breached: 'error',
  paused: 'pause_circle',
  unavailable: 'schedule',
};

const SLA_TEXT: Readonly<Record<SlaVisualState, string>> = {
  healthy: 'On track',
  warning: 'At risk',
  breached: 'Breached',
  paused: 'Paused',
  unavailable: 'No SLA',
};

@Component({
  selector: 'cs-sla-pill',
  imports: [CsIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sla-pill.component.html',
})
export class CsSlaPill {
  readonly state = input<SlaVisualState>('unavailable');
  readonly label = input<string>();

  readonly tone = computed(() => SLA_TONE[this.state()]);
  readonly icon = computed(() => SLA_ICON[this.state()]);
  readonly text = computed(() => this.label() ?? SLA_TEXT[this.state()]);
}
