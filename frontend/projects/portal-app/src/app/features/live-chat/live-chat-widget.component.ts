import { ChangeDetectionStrategy, Component, effect, inject, OnDestroy, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ChatApi,
  ChatMessageDto,
  CsButton,
  CsCard,
  CsDatePipe,
  CsInputField,
  CsIcon,
  LiveChatRealtimeService,
  LocaleStore,
  TranslatePipe,
} from 'common';

@Component({
  selector: 'portal-live-chat-widget',
  imports: [
    CsCard,
    CsButton,
    CsInputField,
    CsIcon,
    ReactiveFormsModule,
    TranslatePipe,
    CsDatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './live-chat-widget.component.html',
})
export default class LiveChatWidgetComponent implements OnDestroy {
  private readonly api = inject(ChatApi);
  private readonly realtime = inject(LiveChatRealtimeService);
  protected readonly locale = inject(LocaleStore);

  readonly started = signal(false);
  readonly starting = signal(false);
  readonly sending = signal(false);
  readonly sessionToken = signal<string | null>(null);
  readonly sessionId = signal<string | null>(null);
  readonly messages = signal<readonly ChatMessageDto[]>([]);
  readonly isClosed = signal(false);

  /** FB-5 — connection states: connecting / connected / reconnecting / disconnected. */
  readonly connectionState = this.realtime.state;

  constructor() {
    // An agent reply pushed to this session's /hubs/chat arrival surfaces here. It is filtered to
    // the active session and deduplicated by id, matching ChatStore.appendMessage.
    effect(() => {
      const incoming = this.realtime.incoming();
      const sId = this.sessionId();
      if (incoming && sId && incoming.sessionId === sId) {
        this.appendMessage(incoming);
      }
    });
  }

  private appendMessage(message: ChatMessageDto): void {
    const current = this.messages();
    if (!current.some((m) => m.id === message.id)) {
      this.messages.set([...current, message]);
    }
  }

  ngOnDestroy(): void {
    void this.realtime.disconnect();
  }

  readonly startForm = new FormGroup({
    customerName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    customerEmail: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    initialMessage: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  readonly messageForm = new FormGroup({
    body: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  startChat(): void {
    if (this.startForm.invalid || this.starting()) {
      return;
    }

    this.starting.set(true);
    const formValue = this.startForm.getRawValue();

    this.api.startAnonymousSession(formValue).subscribe({
      next: (res) => {
        this.starting.set(false);
        this.sessionToken.set(res.sessionToken);
        this.sessionId.set(res.sessionId);
        this.started.set(true);

        const initial: ChatMessageDto = {
          id: `m-init-${Date.now()}`,
          sessionId: res.sessionId,
          senderType: 'Customer',
          senderName: formValue.customerName,
          body: formValue.initialMessage,
          sentAt: new Date().toISOString(),
        };
        this.messages.set([initial]);

        // FB-4 — connect to the session-scoped anonymous hub with the opaque token so agent
        // replies arrive in real time without polling or a reload.
        void this.realtime.connect(res.sessionToken);
      },
      error: () => {
        this.starting.set(false);
      },
    });
  }

  send(): void {
    const token = this.sessionToken();
    if (!token || this.messageForm.invalid || this.sending() || this.isClosed()) {
      return;
    }

    const { body } = this.messageForm.getRawValue();
    this.sending.set(true);

    this.api.sendAnonymousMessage(token, body).subscribe({
      next: (sent) => {
        this.sending.set(false);
        this.appendMessage(sent);
        this.messageForm.reset();
      },
      error: () => {
        this.sending.set(false);
      },
    });
  }

  endChat(): void {
    this.isClosed.set(true);
    void this.realtime.disconnect();
  }
}
