import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AiApi, AiAnswerDto, KbCitationDto } from './ai.api';
import { ApiError } from '../api/api-error';
import { CsButton } from '../ui/button.component';
import { CsCard } from '../ui/card.component';
import { CsIcon } from '../ui/icon.component';
import { LocaleStore } from '../i18n/locale.store';
import { TranslatePipe } from '../i18n/translate.pipe';

interface AssistantMessage {
  readonly role: 'user' | 'bot';
  readonly text: string;
  readonly citations?: readonly KbCitationDto[];
  readonly refusal?: boolean;
}

@Component({
  selector: 'cs-ai-assistant',
  imports: [FormsModule, RouterLink, CsButton, CsCard, CsIcon, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ai-assistant.component.html',
})
export class AiAssistantComponent {
  private readonly ai = inject(AiApi);
  private readonly locale = inject(LocaleStore);

  readonly messages = signal<AssistantMessage[]>([]);
  readonly busy = signal(false);
  readonly question = signal('');

  send(): void {
    const text = this.question().trim();
    if (!text || this.busy()) {
      return;
    }

    this.messages.update((messages) => [...messages, { role: 'user', text }]);
    this.question.set('');
    this.busy.set(true);

    this.ai.ask(text).subscribe({
      next: (answer: AiAnswerDto) => {
        this.messages.update((messages) => [
          ...messages,
          { role: 'bot', text: answer.answer, citations: answer.citations },
        ]);
        this.busy.set(false);
      },
      error: (failure: unknown) => {
        this.messages.update((messages) => [
          ...messages,
          {
            role: 'bot',
            refusal: true,
            text:
              failure instanceof ApiError && failure.code === 'ERR053'
                ? this.locale.t('ai.ungrounded')
                : failure instanceof ApiError
                  ? failure.message_
                  : this.locale.t('ai.error'),
          },
        ]);
        this.busy.set(false);
      },
    });
  }
}
