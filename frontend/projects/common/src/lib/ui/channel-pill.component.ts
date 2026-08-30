import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CsIcon } from './icon.component';

const CHANNEL_TONE: Readonly<Record<string, string>> = {
  system: 'border-slate-200 bg-slate-50 text-slate-700',
  email: 'border-sky-200 bg-sky-50 text-sky-700',
  whatsapp: 'border-emerald-200 bg-emerald-50 text-emerald-700',
  sms: 'border-amber-200 bg-amber-50 text-amber-700',
  webform: 'border-indigo-200 bg-indigo-50 text-indigo-700',
  livechat: 'border-violet-200 bg-violet-50 text-violet-700',
};

const CHANNEL_ICON: Readonly<Record<string, string>> = {
  system: 'settings',
  email: 'mail',
  whatsapp: 'forum',
  sms: 'sms',
  webform: 'article',
  livechat: 'chat',
};

const CHANNEL_LABEL: Readonly<Record<string, string>> = {
  system: 'System',
  email: 'Email',
  whatsapp: 'WhatsApp',
  sms: 'SMS',
  webform: 'Web form',
  livechat: 'Live chat',
};

@Component({
  selector: 'cs-channel-pill',
  imports: [CsIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './channel-pill.component.html',
})
export class CsChannelPill {
  readonly channel = input.required<string>();
  readonly label = input<string>();

  private readonly key = computed(() => this.channel().toLowerCase());
  readonly tone = computed(
    () => CHANNEL_TONE[this.key()] ?? 'border-border-subtle bg-surface-highest text-on-surface-variant',
  );
  readonly icon = computed(() => CHANNEL_ICON[this.key()] ?? 'hub');
  readonly text = computed(() => this.label() ?? CHANNEL_LABEL[this.key()] ?? this.channel());
}
