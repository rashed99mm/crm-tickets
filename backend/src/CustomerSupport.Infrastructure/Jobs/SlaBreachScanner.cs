using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Shared.Contracts;
using CustomerSupport.Shared.Contracts.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CustomerSupport.Infrastructure.Jobs;

/// <summary>
/// One pass of SLA breach detection (FEAT-17, AC-131..AC-133) — split from the hosted service that
/// loops it so a test can call one pass directly, the same reasoning that keeps
/// <c>DbExceptionTranslator</c> and its caller separate.
///
/// Also drives US-218's auto-escalation progression (AC-218.1..AC-218.3): when a ticket records a new
/// breach, the pass advances it to the next configured level and publishes one
/// <c>SlaEscalatedMessage</c>. Level selection happens through <see cref="IEscalationLevelProvider"/>
/// so this class never queries level data itself; the ticket's <c>RowVersion</c> concurrency token
/// plus <see cref="Ticket.AdvanceEscalation"/>'s cursor guard make a concurrent repeated pass a
/// no-op rather than a duplicate transition.
/// </summary>
public interface ISlaBreachScanner
{
    /// <summary>Returns the number of new breach events recorded this pass.</summary>
    Task<int> ScanAsync(CancellationToken ct = default);
}

    public class SlaBreachScanner(
        AppDbContext db,
        IEscalationLevelProvider levels,
        IMessagePublisher publisher,
        INotificationGateway notifications,
        IIdentityUserService users,
        IConfiguration configuration) : ISlaBreachScanner
    {
    /// <summary>
    /// Only `New`/`Open` tickets are evaluated (AC-133). `Waiting for Customer` and `Waiting for
    /// Internal Team` tickets are paused and not evaluated for breach (AC-504). This is also what
    /// makes `AC2183_WaitingOrResolvedTicket_DoesNotEscalate` hold: a ticket outside these statuses
    /// never reaches the escalation step.
    /// </summary>
    private static readonly string[] EvaluatedStatuses = ["New", "Open"];

    private const int MaxRetryAttempts = 3;

    public async Task<int> ScanAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var warningPercentage = Math.Clamp(
            configuration.GetValue("SlaAutomation:WarningPercentage", 0.8), 0.5, 0.99);

        var candidates = await db.Tickets
            .Where(t => EvaluatedStatuses.Contains(t.Status)
                && ((t.ResponseDueAt != null && t.ResponseDueAt <= now.AddDays(1))
                    || (t.ResolutionDueAt != null && t.ResolutionDueAt <= now.AddDays(1))))
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return 0;
        }

        var ticketIds = candidates.Select(t => t.Id).ToList();

        // AC-132 — do not record a duplicate breach for a ticket/target-type pair already recorded.
        var alreadyBreached = (await db.Set<SLAEvent>().IgnoreQueryFilters()
                .Where(e => ticketIds.Contains(e.TicketId) && e.BreachedAt != null)
                .Select(e => new { e.TicketId, e.TargetType })
                .ToListAsync(ct))
            .Select(e => (e.TicketId, e.TargetType))
            .ToHashSet();

        var recorded = 0;
        List<(Ticket Ticket, EscalationLevel Level, string From)> escalated = []; // `From` = the pre-mutation level, for the message
        List<Ticket> breached = []; // US-219 — tickets whose breach event is new this pass
        List<(Ticket Ticket, string TargetType, DateTime DueAt)> warnings = [];

        foreach (var ticket in candidates)
        {
            var newBreach = false;

            AddWarningIfDueSoon(ticket, SLAEvent.TargetTypes.Response, ticket.ResponseDueAt, now, warningPercentage, warnings);
            AddWarningIfDueSoon(ticket, SLAEvent.TargetTypes.Resolution, ticket.ResolutionDueAt, now, warningPercentage, warnings);

            if (ticket.ResponseDueAt is { } responseDue && responseDue < now
                && !alreadyBreached.Contains((ticket.Id, SLAEvent.TargetTypes.Response)))
            {
                db.Set<SLAEvent>().Add(SLAEvent.Record(ticket.Id, SLAEvent.TargetTypes.Response, responseDue, now));
                recorded++;
                newBreach = true;
            }

            if (ticket.ResolutionDueAt is { } resolutionDue && resolutionDue < now
                && !alreadyBreached.Contains((ticket.Id, SLAEvent.TargetTypes.Resolution)))
            {
                db.Set<SLAEvent>().Add(SLAEvent.Record(ticket.Id, SLAEvent.TargetTypes.Resolution, resolutionDue, now));
                recorded++;
                newBreach = true;
            }

            if (!newBreach)
            {
                continue;
            }

            breached.Add(ticket);

            // US-218 — select the next level above the ticket's current state and advance. Terminal
            // is "no higher active level" (the provider returns null), not a magic branch.
            var next = await levels.NextLevelAsync(ticket.EscalationState, ct);
            if (next is null)
            {
                continue;
            }

            var from = ticket.EscalationState;
            try
            {
                ticket.AdvanceEscalation(from, next.Level, SystemActors.EscalationEngine);
                escalated.Add((ticket, next, from));
            }
            catch (InvalidOperationException)
            {
                // Already escalated to this level by a concurrent pass, or a non-forward move —
                // either way the transition must not be duplicated (AC-218.3). No message either.
            }
        }

        if (recorded == 0 && escalated.Count == 0 && warnings.Count == 0)
        {
            return 0;
        }

        await SaveWithConcurrencyRetryAsync(escalated, ct);

        foreach (var (ticket, level, from) in escalated)
        {
            await publisher.PublishAsync(
                Topics.SlaEscalated,
                new SlaEscalatedMessage(ticket.Id, ticket.Reference, from, level.Level, level.TargetRole, level.BreachMinutes, now),
                ct);
        }

        // US-219 — the assignee learns about the breach through the in-app channel: one
        // notification per newly-breached ticket. The scanner's own breach dedupe (AC-132) is what
        // keeps this from firing twice for the same target; the dedupe key scopes the rest to one
        // pass so a retried pass cannot double-notify.
        foreach (var ticket in breached)
        {
            var escalation = escalated.FirstOrDefault(e => e.Ticket.Id == ticket.Id);
            var message = escalation.Ticket is null
                ? $"Ticket {ticket.Reference} breached its SLA target."
                : $"Ticket {ticket.Reference} breached its SLA target and escalated to {escalation.Level.Level}.";
            var targetRole = escalation.Ticket is null ? null : escalation.Level.TargetRole;
            await NotifyTicketStakeholdersAsync(
                ticket,
                "SLA_BREACH",
                "SLA breached",
                message,
                ct,
                targetRole: targetRole);
        }

        foreach (var (ticket, targetType, dueAt) in warnings)
        {
            var correlationId = $"sla-warning:{ticket.Id}:{targetType}";
            var sent = await db.Set<NotificationDelivery>().AsNoTracking()
                .AnyAsync(d => d.TemplateCode == "SLA_WARNING" && d.CorrelationId == correlationId
                    && d.Status != NotificationDelivery.DeliveryStatus.Failed, ct);
            if (sent)
            {
                continue;
            }

            var remaining = Math.Max(0, (int)Math.Ceiling((dueAt - now).TotalMinutes));
            await NotifyTicketStakeholdersAsync(
                ticket,
                "SLA_WARNING",
                "SLA warning",
                $"Ticket {ticket.Reference} is approaching its {targetType.ToLowerInvariant()} SLA target ({remaining} minute(s) remaining).",
                ct,
                correlationId: correlationId);
        }

        return recorded;
    }

    private static void AddWarningIfDueSoon(
        Ticket ticket,
        string targetType,
        DateTime? dueAt,
        DateTime now,
        double warningPercentage,
        List<(Ticket Ticket, string TargetType, DateTime DueAt)> warnings)
    {
        if (dueAt is not { } due || due <= now || due <= ticket.CreatedAt)
        {
            return;
        }

        var totalMinutes = (due - ticket.CreatedAt).TotalMinutes;
        var remainingMinutes = (due - now).TotalMinutes;
        if (remainingMinutes <= totalMinutes * (1 - warningPercentage))
        {
            warnings.Add((ticket, targetType, due));
        }
    }

    private async Task NotifyTicketStakeholdersAsync(
        Ticket ticket,
        string templateCode,
        string title,
        string message,
        CancellationToken ct,
        string? targetRole = null,
        string? correlationId = null)
    {
        var recipients = new HashSet<Guid>();
        if (ticket.AssigneeId is { } assigneeId)
        {
            recipients.Add(assigneeId);
        }

        foreach (var supervisor in await users.GetUsersInRoleAsync(ApplicationRole.Roles.Supervisor, ct))
        {
            recipients.Add(supervisor.Id);
        }

        if (!string.IsNullOrWhiteSpace(targetRole))
        {
            foreach (var target in await users.GetUsersInRoleAsync(targetRole, ct))
            {
                recipients.Add(target.Id);
            }
        }

        foreach (var recipient in recipients)
        {
            await notifications.SendAsync(new NotificationDispatchRequest(
                TemplateCode: templateCode,
                RecipientUserId: recipient,
                Channels: [NotificationChannel.InApp],
                Variables: new Dictionary<string, string> { ["Title"] = title, ["Message"] = message },
                Email: null,
                PhoneNumber: null,
                BypassUserSettings: true,
                DeduplicationKey: $"{templateCode.ToLowerInvariant()}:{ticket.Id}:{recipient}",
                CorrelationId: correlationId ?? $"sla-breach:{ticket.Id}"), ct);
        }
    }

    /// <summary>
    /// Persists the pass in one transaction, retrying a bounded number of times when a concurrent
    /// pass wins the <c>RowVersion</c> race on the affected tickets. On each conflict the affected
    /// tickets are reloaded: if one now sits at the level this pass intended, the transition was
    /// already applied by the winner, so the staged history row is dropped and the ticket removed
    /// from the message list — the no-op AC-218.3 asks for.
    /// </summary>
    private async Task SaveWithConcurrencyRetryAsync(List<(Ticket Ticket, EscalationLevel Level, string From)> escalated, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < MaxRetryAttempts)
            {
                await ResolveConcurrentEscalationsAsync(ex, escalated, ct);
            }
        }
    }

    private async Task ResolveConcurrentEscalationsAsync(
        DbUpdateConcurrencyException ex,
        List<(Ticket Ticket, EscalationLevel Level, string From)> escalated,
        CancellationToken ct)
    {
        var affected = ex.Entries
            .Where(e => e.Entity is Ticket)
            .Select(e => (Ticket)e.Entity)
            .ToHashSet();

        foreach (var entry in ex.Entries.Where(e => e.Entity is Ticket))
        {
            // Reload replaces the ticket's scalar values (including EscalationState and RowVersion)
            // with the committed values, discarding our in-memory advancement.
            await entry.ReloadAsync(ct);
        }

        // Drop any Escalated history row staged for a conflicted ticket that did not win, so no
        // orphan history is written when the retry saves. A ticket stages at most one such row per
        // pass (the escalation guard refuses a second), so match on TicketId alone is safe.
        foreach (var entry in db.ChangeTracker.Entries<TicketHistory>()
                     .Where(e => e.State == EntityState.Added
                         && e.Entity.ChangeType == TicketChangeType.Escalated.Value
                         && affected.Any(t => t.Id == e.Entity.TicketId))
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        escalated.RemoveAll(item =>
            affected.Contains(item.Ticket) && item.Ticket.EscalationState == item.Level.Level);
    }
}
