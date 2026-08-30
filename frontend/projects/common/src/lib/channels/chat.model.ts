export type ChatSessionStatus = 'Waiting' | 'Active' | 'Closed' | 'Abandoned';

export interface ChatSessionDto {
  readonly id: string;
  readonly customerName?: string | null;
  readonly customerEmail?: string | null;
  readonly status: ChatSessionStatus;
  readonly priority: string;
  readonly type: string;
  readonly createdAt: string;
  readonly claimedAt?: string | null;
  readonly claimedByAgentId?: string | null;
  readonly claimedByAgentName?: string | null;
  readonly closedAt?: string | null;
}

export type ChatSessionPriority = 'Normal' | 'High' | 'Urgent';

export interface ChatMessageDto {
  readonly id: string;
  readonly sessionId: string;
  readonly senderType: 'Customer' | 'Agent' | 'System';
  readonly senderName: string;
  readonly senderId?: string | null;
  readonly body: string;
  readonly sentAt: string;
}

export interface StartChatSessionRequest {
  readonly customerName?: string;
  readonly customerEmail?: string;
  readonly initialMessage?: string;
}

export interface StartChatSessionResponse {
  readonly sessionToken: string;
  readonly sessionId: string;
}

export interface SendChatMessageRequest {
  readonly body: string;
}

export interface ChatReplySuggestionDto {
  readonly drafts: readonly string[];
  readonly summary: string;
}
