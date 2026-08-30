import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '../i18n/translate.pipe';
import { AiChatApi, AiChatDto, AiChatTurnDto } from './chat.api';

/**
 * AI-47 — the multi-turn assistant conversation, shared by the staff shell and the portal.
 * Signals carry the whole state; the degraded (ERR052 → 503) and ungrounded states render
 * distinctly instead of as a generic error, matching how the panel treats them.
 */
@Component({
  selector: 'cs-ai-chat-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './ai-chat-panel.component.html',
})
export class AiChatPanelComponent {
  private readonly api = inject(AiChatApi);

  readonly turns = signal<readonly AiChatTurnDto[]>([]);
  readonly sessionId = signal<string | null>(null);
  readonly status = signal<'Open' | 'Closed'>('Open');
  readonly draft = signal('');

  readonly busy = signal(false);
  readonly degraded = signal(false); // 503/ERR052 — feature off in this deployment
  readonly failed = signal(false); // 503/ERR070/ERR071 — provider chain failed

  readonly canSend = computed(
    () => !this.busy() && this.status() === 'Open' && this.draft().trim().length > 0,
  );

  send(): void {
    const message = this.draft().trim();
    if (!this.canSend() || message.length === 0) {
      return;
    }

    this.draft.set('');
    this.busy.set(true);
    this.failed.set(false);

    const existing = this.sessionId();
    const call = existing === null ? this.api.start(message) : this.api.send(existing, message);

    call.subscribe({
      next: (chat: AiChatDto) => {
        this.degraded.set(false);
        this.busy.set(false);
        this.sessionId.set(chat.sessionId);
        this.status.set(chat.status);
        this.turns.set(chat.turns);
      },
      error: () => {
        this.busy.set(false);
        this.failed.set(true);
      },
    });
  }

  handoff(): void {
    const id = this.sessionId();
    if (id === null || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.api.handoff(id).subscribe({
      next: () => {
        this.busy.set(false);
        this.status.set('Closed');
      },
      error: () => {
        this.busy.set(false);
        this.failed.set(true);
      },
    });
  }
}
