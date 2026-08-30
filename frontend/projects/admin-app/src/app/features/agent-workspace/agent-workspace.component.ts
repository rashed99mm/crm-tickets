import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  ApiError,
  AsyncState,
  ChatApi,
  ChatSessionDto,
  CsBadge,
  CsCard,
  CsDatePipe,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  CsStatusPill,
  LocaleStore,
  NotificationStore,
  RealtimeService,
  TicketApi,
  TicketListItem,
  ToastService,
  TranslatePipe,
  empty,
  failed,
  loaded,
  loading,
} from 'common';

interface QuickReplyPreset {
  readonly key:
    | 'dashboard.quickReplies.investigating'
    | 'dashboard.quickReplies.needInfo'
    | 'dashboard.quickReplies.resolved';
  readonly icon: string;
}

const QUICK_REPLIES: readonly QuickReplyPreset[] = [
  { key: 'dashboard.quickReplies.investigating', icon: 'search' },
  { key: 'dashboard.quickReplies.needInfo', icon: 'info' },
  { key: 'dashboard.quickReplies.resolved', icon: 'task_alt' },
];

@Component({
  selector: 'admin-agent-workspace',
  imports: [
    RouterLink,
    CsBadge,
    CsCard,
    CsDatePipe,
    CsEmptyState,
    CsErrorState,
    CsIcon,
    CsLoadingState,
    CsStatusPill,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './agent-workspace.component.html',
})
export default class AgentWorkspaceComponent {
  private readonly ticketsApi = inject(TicketApi);
  private readonly chatApi = inject(ChatApi);
  private readonly notifications = inject(NotificationStore);
  protected readonly realtime = inject(RealtimeService);
  protected readonly locale = inject(LocaleStore);
  private readonly toast = inject(ToastService);

  readonly ticketsState = signal<AsyncState<readonly TicketListItem[]>>(loading());
  readonly chatState = signal<AsyncState<readonly ChatSessionDto[]>>(loading());
  protected readonly quickReplies = QUICK_REPLIES;

  readonly tickets = computed(() => {
    const current = this.ticketsState();
    return current.status === 'loaded' ? current.data : [];
  });

  readonly chats = computed(() => {
    const current = this.chatState();
    return current.status === 'loaded' ? current.data : [];
  });

  readonly ticketError = computed<ApiError | null>(() => {
    const current = this.ticketsState();
    return current.status === 'error' ? current.error : null;
  });

  readonly chatError = computed<ApiError | null>(() => {
    const current = this.chatState();
    return current.status === 'error' ? current.error : null;
  });

  readonly openTicketCount = computed(
    () =>
      this.tickets().filter(
        (ticket) => ticket.status !== 'Resolved' && ticket.status !== 'Closed',
      ).length,
  );

  readonly escalatedCount = computed(
    () => this.tickets().filter((ticket) => ticket.escalationState !== 'None').length,
  );

  readonly notificationItems = computed(() => this.notifications.items().slice(0, 5));
  readonly unreadCount = computed(() => this.notifications.unreadCount());

  constructor() {
    this.loadTickets();
    this.loadChats();
  }

  loadTickets(): void {
    this.ticketsState.set(loading());
    this.ticketsApi.list({ mine: true, page: 1, pageSize: 6 }).subscribe({
      next: (page) =>
        this.ticketsState.set(page.items.length === 0 ? empty() : loaded(page.items)),
      error: (error: unknown) => this.ticketsState.set(failed(this.toApiError(error))),
    });
  }

  loadChats(): void {
    this.chatState.set(loading());
    this.chatApi.getWaitingSessions().subscribe({
      next: (result) => {
        const sessions = result.items;
        this.chatState.set(sessions.length === 0 ? empty() : loaded(sessions.slice(0, 6)));
      },
      error: (error: unknown) => this.chatState.set(failed(this.toApiError(error))),
    });
  }

  async copyQuickReply(key: QuickReplyPreset['key']): Promise<void> {
    const text = this.locale.t(key);
    try {
      await navigator.clipboard.writeText(text);
      this.toast.success(this.locale.t('workspace.quickReplyCopied'), text);
    } catch {
      this.toast.error(this.locale.t('workspace.quickReplyCopyFailed'));
    }
  }

  connectionLabel(): string {
    switch (this.realtime.connectionState()) {
      case 'connected':
        return this.locale.t('workspace.status.connected');
      case 'connecting':
        return this.locale.t('workspace.status.connecting');
      case 'reconnecting':
        return this.locale.t('workspace.status.reconnecting');
      default:
        return this.locale.t('workspace.status.disconnected');
    }
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError
      ? error
      : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
