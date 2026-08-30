import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  OnInit,
  signal,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  ApiError,
  ChatApi,
  ChatStore,
  CsButton,
  CsDatePipe,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  LocaleStore,
  RealtimeService,
  TranslatePipe,
} from 'common';

@Component({
  selector: 'admin-chat-session',
  imports: [
    CsIcon,
    CsLoadingState,
    CsEmptyState,
    CsErrorState,
    CsButton,
    ReactiveFormsModule,
    TranslatePipe,
    CsDatePipe,
    RouterLink,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './chat-session.component.html',
})
export default class ChatSessionComponent implements OnInit {
  private readonly api = inject(ChatApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  protected readonly store = inject(ChatStore);
  protected readonly realtime = inject(RealtimeService);
  protected readonly locale = inject(LocaleStore);

  readonly id = input.required<string>();
  readonly closing = signal(false);
  readonly customerName = computed(() => {
    const customer = this.store.messages().find((message) => message.senderType === 'Customer');
    return customer?.senderName || 'Customer';
  });

  readonly conversationStartedAt = computed(() => this.store.messages()[0]?.sentAt ?? null);
  readonly lastCustomerMessage = computed(() => {
    const customerMessages = this.store.messages().filter((message) => message.senderType === 'Customer');
    return customerMessages.at(-1)?.body ?? '';
  });

  readonly contextSummary = computed(() => {
    const messages = this.store.messages();
    const customerCount = messages.filter((message) => message.senderType === 'Customer').length;
    const agentCount = messages.filter((message) => message.senderType === 'Agent').length;
    const latest = this.lastCustomerMessage();
    if (messages.length === 0) {
      return 'No transcript has been recorded yet.';
    }
    return `${customerCount} customer message(s), ${agentCount} agent reply/replies. Latest customer note: ${latest || 'none yet'}`;
  });

  readonly aiLoading = signal(false);
  readonly aiError = signal<string | null>(null);
  readonly aiSummary = signal<string | null>(null);
  readonly aiDrafts = signal<readonly string[]>([]);

  readonly messageForm = new FormGroup({
    body: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  ngOnInit(): void {
    this.store.initSession(this.id());
    this.destroyRef.onDestroy(() => {
      this.store.destroy();
    });
  }

  loadAiSuggestions(): void {
    if (this.aiLoading()) {
      return;
    }

    this.aiLoading.set(true);
    this.aiError.set(null);
    this.api.suggestReply(this.id()).subscribe({
      next: (suggestion) => {
        this.aiDrafts.set(suggestion.drafts);
        this.aiSummary.set(suggestion.summary);
        this.aiLoading.set(false);
      },
      error: (err: unknown) => {
        const fallback = err instanceof ApiError ? err.message_ : this.locale.t('chat.workspace.aiFailed');
        this.aiError.set(fallback);
        this.aiLoading.set(false);
      },
    });
  }

  useAiAssist(): void {
    const [firstDraft] = this.aiDrafts();
    if (firstDraft) {
      this.insertDraft(firstDraft);
      return;
    }

    this.loadAiSuggestions();
  }

  send(): void {
    if (this.messageForm.invalid || this.store.sending()) {
      return;
    }

    const { body } = this.messageForm.getRawValue();
    this.store.sendMessage(body);
    this.messageForm.reset();
  }

  insertDraft(draft: string): void {
    this.messageForm.controls.body.setValue(draft);
  }

  initials(name: string | null | undefined): string {
    const parts = (name ?? '')
      .trim()
      .split(/\s+/)
      .filter((part) => part.length > 0);
    const initials = parts.slice(0, 2).map((part) => part[0]?.toUpperCase()).join('');
    return initials || '?';
  }

  closeSession(): void {
    if (this.closing()) {
      return;
    }

    this.closing.set(true);
    this.api.closeSession(this.id()).subscribe({
      next: () => {
        this.closing.set(false);
        this.router.navigate(['/chat']);
      },
      error: () => {
        this.closing.set(false);
      },
    });
  }
}
