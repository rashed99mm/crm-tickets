/*
 * Public API of the `common` library.
 *
 * Both apps import from here. Anything not exported is internal.
 */

// API — the response envelope contract and its single unwrap point.
export * from './lib/api/api-response';
export * from './lib/api/api-error';
export * from './lib/api/envelope.interceptor';

// Auth — session signals derived from the token, plus route guards.
export * from './lib/auth/token-storage';
export * from './lib/auth/session.store';
export * from './lib/auth/auth.api';
export * from './lib/auth/staff.api';
export * from './lib/auth/auth.interceptor';
export * from './lib/auth/refresh.interceptor';
export * from './lib/auth/guards';

// i18n — one locale signal drives text, document lang and direction.
export * from './lib/i18n/locale.store';
export * from './lib/i18n/localize.pipe';
export * from './lib/i18n/translations';
export * from './lib/i18n/translate.pipe';
export * from './lib/i18n/date.pipe';

// Tickets — the queue and capture API.
export * from './lib/tickets/ticket.api';
export * from './lib/tickets/sla-countdown.component';

// Customer portal — the portal surface API (FEAT-22).
export * from './lib/portal/portal.api';

// Communication Channels — WhatsApp, SMS, Live chat, Web forms (FEAT-24..27).
export * from './lib/channels/channel-labels';
export * from './lib/channels/chat.model';
export * from './lib/channels/chat.api';
export * from './lib/channels/chat.store';
export * from './lib/channels/live-chat-realtime.service';
export * from './lib/channels/web-form.api';
// Customers — the customer records and their interaction history.
export * from './lib/customers/customer.api';

// Contents — the knowledge base (customer-facing help articles).
export * from './lib/contents/contents.api';

// Knowledge-base administration — the staff authoring surface (FEAT-18, US-509..512).
export * from './lib/admin/kb-admin.api';

// AI assist — the drafting client and the grounded QA call (FEAT-21).
export * from './lib/ai/ai.api';
export * from './lib/ai/ai-assistant.component';

export * from './lib/organisation/organisation.api';
export * from './lib/organisation/sla-policy.api';

export * from './lib/admin/audit-log.api';
export * from './lib/admin/platform-setting.api';
export * from './lib/admin/cms-integration.api';
export * from './lib/admin/branding.api';
export * from './lib/admin/branding.store';
export * from './lib/admin/permission.api';

// Notifications — the client in-app inbox and the backend notifications API (FEAT-15).
export * from './lib/notifications/notification.model';
export * from './lib/notifications/notification.store';
export * from './lib/notifications/notification.api';

// Upload — global shared upload service.
export * from './lib/upload/upload.service';

// Reports — FEAT-19+ frontend addendum, the three shipped read-only reports.
export * from './lib/reports/report.api';
export * from './lib/reports/report-date-range-filter.component';

// State — async work as a closed union, so empty and error stay distinct.
export * from './lib/state/async-state';

// UI — presentational components shared by both apps.
export * from './lib/ui/loading-state.component';
export * from './lib/ui/empty-state.component';
export * from './lib/ui/error-state.component';
export * from './lib/ui/icon.component';
export * from './lib/ui/card.component';
export * from './lib/ui/dialog.component';
export * from './lib/ui/confirmation.service';
export * from './lib/ui/confirmation-host.component';
export * from './lib/ui/toast.service';
export * from './lib/ui/toast-host.component';
export * from './lib/ui/button.component';
export * from './lib/ui/badge.component';
export * from './lib/ui/status-pill.component';
export * from './lib/ui/channel-pill.component';
export * from './lib/ui/sla-pill.component';
export * from './lib/ui/action-bar.component';
export * from './lib/ui/chart-frame.component';
export * from './lib/ui/stat-card.component';
export * from './lib/ui/placeholder.component';
export * from './lib/ui/pagination.component';
export * from './lib/ui/data-toolbar.component';
export * from './lib/ui/initials';
export * from './lib/ui/input-field.component';
export * from './lib/ui/language-switcher.component';
export * from './lib/ui/attachment-picker.component';
export * from './lib/ui/attachment-list.component';

// Realtime — SignalR client, inert until a hub url is configured.
export * from './lib/realtime/realtime.config';
export * from './lib/realtime/realtime.service';

// AI multi-turn chatbot (provider abstraction enhancement).
export * from './lib/ai/chat.api';
export * from './lib/ai/ai-chat-panel.component';
export * from './lib/ai/kb-ai-sidebar.component';
