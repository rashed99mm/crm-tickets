import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  AiApi,
  ApiError,
  CsButton,
  CsCard,
  CsIcon,
  KbCitationDto,
  LocaleStore,
  TranslatePipe,
} from 'common';

interface ChatMessage {
  readonly role: 'user' | 'bot';
  readonly text: string;
  readonly citations?: readonly KbCitationDto[];
  /** A3 â€” refusals render as "ask a human" copy, visually distinct from a grounded answer. */
  readonly refusal?: boolean;
}

/**
 * FEAT-21 QA behaviour (AC-F7 / spec A3) â€” the customer's grounded chat panel.
 * One question in flight at a time; every bot answer carries its KB citations; the ERR053
 * ungrounded refusal renders dictionary copy instead of an apology invented client-side.
 */
@Component({
  selector: 'portal-chat',
  imports: [FormsModule, RouterLink, CsCard, CsIcon, CsButton, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './chat.component.html',
})
export class PortalChatComponent {
  private readonly ai = inject(AiApi);
  private readonly locale = inject(LocaleStore);

  readonly messages = signal<ChatMessage[]>([]);
  readonly busy = signal(false);
  readonly question = signal('');

  send(): void {
    const text = this.question().trim();
    if (!text || this.busy()) {
      return;
    }

    this.messages.update((m) => [...m, { role: 'user', text }]);
    this.question.set('');
    this.busy.set(true);

    this.ai.ask(text).subscribe({
      next: (answer) => {
        this.messages.update((m) => [
          ...m,
          { role: 'bot', text: answer.answer, citations: answer.citations },
        ]);
        this.busy.set(false);
      },
      error: (failure: unknown) => {
        this.messages.update((m) => [
          ...m,
          {
            role: 'bot',
            refusal: true,
            text:
              failure instanceof ApiError && failure.code === 'ERR053'
                ? this.locale.t('ai.ungrounded')
                : failure instanceof ApiError
                  ? failure.message_
                  : 'Something went wrong',
          },
        ]);
        this.busy.set(false);
      },
    });
  }
}
