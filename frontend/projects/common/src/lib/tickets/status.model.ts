/**
 * The eight ticket lifecycle states — exactly matching the backend `TicketStatus` value object
 * (AC-501, AC-532). No server authority is assumed here; the server is always the source of
 * truth for what transitions are currently permitted.
 */
export type TicketStatusValue =
  | 'New'
  | 'Open'
  | 'Assigned'
  | 'In Progress'
  | 'Waiting for Customer'
  | 'Waiting for Internal Team'
  | 'Resolved'
  | 'Closed';

/** The eight statuses in lifecycle order. */
export const TICKET_STATUS_VALUES: readonly TicketStatusValue[] = [
  'New',
  'Open',
  'Assigned',
  'In Progress',
  'Waiting for Customer',
  'Waiting for Internal Team',
  'Resolved',
  'Closed',
];

/**
 * Tailwind classes for a solid-fill status badge (used in headers and dense chips).
 * Matches `badge.component.ts` `STATUS_TONE`.  Every key is a literal; Tailwind scans
 * source text for class names so runtime assembly would drop styles in production builds.
 */
export const STATUS_TONE: Readonly<Record<TicketStatusValue, string>> = {
  new: 'bg-status-new text-on-primary',
  open: 'bg-status-open text-on-primary',
  assigned: 'bg-status-assigned text-on-primary',
  'in progress': 'bg-status-in-progress text-on-primary',
  'waiting for customer': 'bg-status-waiting-for-customer text-on-primary',
  'waiting for internal team': 'bg-status-waiting-for-internal-team text-on-primary',
  resolved: 'bg-status-resolved text-on-primary',
  closed: 'bg-status-closed text-on-primary',
  escalated: 'bg-status-escalated text-on-primary',
};

/**
 * Tailwind classes for a tinted-outlined status pill (used in table rows beside priority pills).
 * Matches `status-pill.component.ts` `STATUS_TINT` + `STATUS_DOT`.
 */
export const STATUS_TINT: Readonly<Record<TicketStatusValue, string>> = {
  new: 'bg-status-new/10 text-status-new border border-status-new/20',
  open: 'bg-status-open/10 text-status-open border border-status-open/20',
  assigned: 'bg-status-assigned/10 text-status-assigned border border-status-assigned/20',
  'in progress': 'bg-status-in-progress/10 text-status-in-progress border border-status-in-progress/20',
  'waiting for customer': 'bg-status-waiting-for-customer/10 text-status-waiting-for-customer border border-status-waiting-for-customer/20',
  'waiting for internal team': 'bg-status-waiting-for-internal-team/10 text-status-waiting-for-internal-team border border-status-waiting-for-internal-team/20',
  resolved: 'bg-status-resolved/10 text-status-resolved border border-status-resolved/20',
  closed: 'bg-status-closed/10 text-status-closed border border-status-closed/20',
  escalated: 'bg-status-escalated/10 text-status-escalated border border-status-escalated/20',
};

export const STATUS_DOT: Readonly<Record<TicketStatusValue, string>> = {
  new: 'bg-status-new',
  open: 'bg-status-open',
  assigned: 'bg-status-assigned',
  'in progress': 'bg-status-in-progress',
  'waiting for customer': 'bg-status-waiting-for-customer',
  'waiting for internal team': 'bg-status-waiting-for-internal-team',
  resolved: 'bg-status-resolved',
  closed: 'bg-status-closed',
  escalated: 'bg-status-escalated',
};

/**
 * The server's transition table (AC-501), mirrored so the UI can grey out unavailable actions
 * without a round-trip.  The server remains the authority; a drifted client still gets 409.
 */
export const PERMITTED_TRANSITIONS: Readonly<Record<TicketStatusValue, readonly TicketStatusValue[]>> = {
  New: ['Open'],
  Open: ['Assigned', 'Resolved'],
  Assigned: ['In Progress'],
  'In Progress': ['Waiting for Customer', 'Waiting for Internal Team', 'Resolved'],
  'Waiting for Customer': ['In Progress'],
  'Waiting for Internal Team': ['In Progress'],
  Resolved: ['In Progress', 'Closed'],
  Closed: ['In Progress'],
};
