import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, interval, Subscription } from 'rxjs';
import {
  ApiError,
  AssignableAgent,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsIcon,
  CsLoadingState,
  TicketApi,
  TicketListItem,
  TranslatePipe,
} from 'common';

/** Tickets waiting longer than this are flagged urgent in the unassigned queue (US-607). */
const URGENT_THRESHOLD_MINUTES = 30;

interface LiveQueueRow {
  readonly id: string;
  readonly reference: string;
  readonly subject: string;
  readonly customerName: string;
  readonly priority: string;
  readonly status: string;
  readonly createdAt: string;
  readonly waitMinutes: number;
  readonly urgent: boolean;
}

interface AgentLoadRow {
  readonly agentId: string;
  readonly name: string;
  readonly openCount: number;
}

/** US-607 — the live operational queue: unassigned work (oldest first, urgent flagged) and per-agent load. */
@Component({
  selector: 'admin-live-queue',
  imports: [RouterLink, CsCard, CsLoadingState, CsEmptyState, CsErrorState, CsIcon, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './live-queue.component.html',
})
export default class LiveQueueComponent {
  private static readonly OPEN_QUEUE_PAGE_SIZE = 100;
  private readonly tickets = inject(TicketApi);
  private readonly destroyRef = inject(DestroyRef);
  private timer?: Subscription;

  readonly loading = signal(false);
  readonly error = signal<ApiError | null>(null);
  readonly unassigned = signal<readonly LiveQueueRow[]>([]);
  readonly agentLoad = signal<readonly AgentLoadRow[]>([]);

  readonly hasUnassigned = computed(() => this.unassigned().length > 0);
  readonly hasAgentLoad = computed(() => this.agentLoad().length > 0);
  readonly urgentCount = computed(() => this.unassigned().filter((row) => row.urgent).length);
  readonly openCount = computed(() => this.agentLoad().reduce((total, row) => total + row.openCount, 0));
  readonly activeAgents = computed(() => this.agentLoad().filter((row) => row.openCount > 0).length);

  constructor() {
    this.load();
    // Live refresh: the queue is only useful if it reflects the floor (DSH-2).
    this.timer = interval(60_000).subscribe(() => this.load());
    this.destroyRef.onDestroy(() => this.timer?.unsubscribe());
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      unassigned: this.tickets.list({ unassigned: true, pageSize: 50 }),
      open: this.tickets.list({ status: 'Open', pageSize: LiveQueueComponent.OPEN_QUEUE_PAGE_SIZE }),
      agents: this.tickets.listAssignableAgents(),
    }).subscribe({
      next: ({ unassigned, open, agents }) => {
        this.unassigned.set(this.toUnassignedRows(unassigned.items));
        this.agentLoad.set(this.toAgentLoad(open.items, agents));
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.error.set(error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0));
        this.loading.set(false);
      },
    });
  }

  private toUnassignedRows(items: readonly TicketListItem[]): LiveQueueRow[] {
    const now = Date.now();
    return items
      .map((item) => {
        const waitMinutes = Math.max(0, Math.round((now - new Date(item.createdAt).getTime()) / 60_000));
        return {
          id: item.id,
          reference: item.reference,
          subject: item.subject,
          customerName: item.customerName,
          priority: item.priority,
          status: item.status,
          createdAt: item.createdAt,
          waitMinutes,
          urgent: waitMinutes >= URGENT_THRESHOLD_MINUTES,
        } as LiveQueueRow;
      })
      .sort((a, b) => b.waitMinutes - a.waitMinutes);
  }

  private toAgentLoad(items: readonly TicketListItem[], agents: readonly AssignableAgent[]): AgentLoadRow[] {
    const counts = new Map<string, number>();
    for (const ticket of items) {
      if (ticket.assigneeId) {
        counts.set(ticket.assigneeId, (counts.get(ticket.assigneeId) ?? 0) + 1);
      }
    }
    return agents
      .map((agent) => ({ agentId: agent.id, name: agent.name, openCount: counts.get(agent.id) ?? 0 }))
      .sort((a, b) => b.openCount - a.openCount);
  }
}
