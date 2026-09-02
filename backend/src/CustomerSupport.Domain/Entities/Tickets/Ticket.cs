using CustomerSupport.Domain.Events.Tickets;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Domain.Entities.Tickets;

/// <summary>
/// A tracked customer request, and the aggregate that owns the lifecycle rules (AC-29..AC-50).
///
/// <see cref="Status"/> and <see cref="AssigneeId"/> have private setters on purpose. The transition
/// table in <see cref="TicketStatus"/> is only an invariant if every path to a status change runs
/// through <see cref="ChangeStatus"/>; a public setter would let a handler bypass it, and eventually
/// one would.
/// </summary>
public class Ticket : AggregateRoot
{
    private readonly List<TicketHistory> _history = [];

    /// <summary>Human-readable <c>TKT-nnnnnn</c>. "Ticket 4192" is not something a person reads aloud.</summary>
    public string Reference { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid CategoryId { get; private set; }

    /// <summary>
    /// Organisational grouping (FEAT-16, AC-117). Nullable and unset by every path today — nothing
    /// in this sprint assigns a ticket to a department or branch; the column exists so a later
    /// feature has something to filter on without another migration.
    /// </summary>
    public Guid? DepartmentId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? TeamId { get; private set; }

    /// <summary>
    /// Lifecycle timestamps for BI (US-906, AC-510, spec A5). Stamped, never derived: first/last
    /// response by <see cref="RecordResponse"/>; resolved/closed on the transitions into those statuses
    /// and cleared on reopen (Task 5 completes those). Null until the event happens — a report must
    /// never read a zero.
    /// </summary>
    public DateTime? FirstResponseAt { get; private set; }
    public DateTime? LastResponseAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    /// <summary>
    /// The channel a ticket originated on ("WhatsApp", "SMS", "WebForm", ...), set once at creation by
    /// the ingestion handler. Null for tickets created by an agent on the staff surface.
    ///
    /// Backs the CC-2/CC-11 "one open ticket per (customer, channel)" rule: without it the open
    /// ticket for a customer/channel could not be queried without a join through <c>TicketMessage</c>,
    /// which would match any channel a ticket ever had a message on rather than the one it started on.
    /// </summary>
    public string? Source { get; private set; }

    /// <summary>
    /// SLA due dates (FEAT-17, AC-128). Null when no active <c>SLAPolicy</c> matched at creation
    /// (AC-129) — never computed retroactively.
    /// </summary>
    public DateTime? ResponseDueAt { get; private set; }
    public DateTime? ResolutionDueAt { get; private set; }

    /// <summary>
    /// SLA pause tracking (FEAT-17 second slice, AC-134..AC-136). <see cref="PausedAt"/> is set
    /// while the ticket sits in <c>Pending</c> and cleared when it leaves; <see cref="TotalPausedSeconds"/>
    /// accumulates across every pause cycle.
    /// </summary>
    public DateTime? PausedAt { get; private set; }
    public int TotalPausedSeconds { get; private set; }

    /// <summary>
    /// One of <c>None</c>/<c>Warning</c>/<c>Level1</c>/<c>Level2</c>/<c>Level3</c> (`BR-32`). Only
    /// <c>None</c> and <c>Level1</c> are actually reachable this slice (spec A2) — the rest exist so
    /// the column does not need to change shape when level progression is built.
    /// </summary>
    public string EscalationState { get; private set; } = "None";

    /// <summary>
    /// The Supervisor/Specialist holding an escalated ticket (US-904, AC-506). Null while the ticket is
    /// not escalated. A marker field beside <see cref="EscalationState"/> — escalation is never a status.
    /// </summary>
    public Guid? EscalationAssigneeId { get; private set; }

    /// <summary>
    /// US-922 / AC-922.2. How the ticket was resolved — required on the transition into
    /// <c>Resolved</c>, cleared on reopen. Null on a ticket that has never been resolved.
    /// </summary>
    public string? ResolutionCode { get; private set; }
    public string? ResolutionNotes { get; private set; }

    /// <summary>US-922 / AC-922.4. How many times a resolved/closed ticket was sent back.</summary>
    public int ReopenCount { get; private set; }

    /// <summary>
    /// US-923. The matrix inputs. Null on tickets created before FEAT-32 (spec A1) — their stored
    /// Priority stands until the first Reclassify.
    /// </summary>
    public string? Impact { get; private set; }
    public string? Urgency { get; private set; }

    public string Priority { get; private set; } = TicketPriority.Normal.Value;
    public string Status { get; private set; } = TicketStatus.New.Value;

    /// <summary>Null is the unassigned queue state (AC-29), not a missing value.</summary>
    public Guid? AssigneeId { get; private set; }

    /// <summary>Optimistic concurrency (AC-41). Two agents resolving the same ticket is ordinary.</summary>
    public byte[]? RowVersion { get; private set; }

    /// <summary>Append-only; the collection is exposed read-only for the same reason.</summary>
    public IReadOnlyCollection<TicketHistory> History => _history.AsReadOnly();

    public static Ticket Create(
        string reference,
        string subject,
        string description,
        Guid customerId,
        Guid categoryId,
        string impact,
        string urgency,
        Guid actorId)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("Reference is required", nameof(reference));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Subject is required", nameof(subject));
        }

        if (subject.Length > 200)
        {
            throw new ArgumentException("Subject must not exceed 200 characters", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required", nameof(description));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A customer is required", nameof(customerId));
        }

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("A category is required", nameof(categoryId));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(actorId));
        }

        var impactVo = TicketImpact.Create(impact);
        var urgencyVo = TicketUrgency.Create(urgency);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Reference = reference.Trim(),
            Subject = subject.Trim(),
            Description = description,
            CustomerId = customerId,
            CategoryId = categoryId,
            Impact = impactVo.Value,
            Urgency = urgencyVo.Value,
            Priority = PriorityMatrix.Derive(impactVo, urgencyVo).Value,
            Status = TicketStatus.New.Value,
            AssigneeId = null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorId
        };

        ticket.Append(actorId, TicketChangeType.Created, null, ticket.Status);
        ticket.AddDomainEvent(new TicketCreatedEvent(ticket.Id, ticket.Reference, ticket.CustomerId, actorId));

        return ticket;
    }

    /// <summary>
    /// Moves the ticket along the lifecycle, or refuses (AC-37..AC-40).
    ///
    /// A refusal throws <see cref="InvalidOperationException"/> rather than returning a validation
    /// failure, because the request was well-formed and it is the state that is wrong — which is
    /// what makes AC-38 a 409 and not a 400.
    /// </summary>
    public void ChangeStatus(string targetStatus, Guid actorId, ResolutionDetails? resolution = null)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(actorId));
        }

        var current = TicketStatus.Create(Status);
        var target = TicketStatus.Create(targetStatus);

        if (!current.CanTransitionTo(target))
        {
            throw new InvalidOperationException(
                $"Cannot change ticket status from '{current.Value}' to '{target.Value}'.");
        }

        // AC-505: a work state cannot be entered without an assignee. The guard is in the aggregate so
        // the existing handler pre-check surfaces it as a 409 without adding a new refusal shape (D2).
        if (target.IsWorkState() && AssigneeId is null)
        {
            throw new InvalidOperationException(
                $"Ticket '{Reference}' must be assigned before it can be '{target.Value}'.");
        }

        var isReopen = current.IsReopenTo(target);
        var changeType = isReopen ? TicketChangeType.Reopened : TicketChangeType.StatusChanged;

        // US-906 / AC-510: entering Resolved/Closed stamps the respective timestamp; reopening clears
        // both so the next resolve starts clean. US-922: the resolution record follows the same
        // lifecycle — required to enter Resolved (AC-922.5), cleared and counted on reopen (AC-922.4).
        if (isReopen)
        {
            ResolvedAt = null;
            ClosedAt = null;
            ResolutionCode = null;
            ResolutionNotes = null;
            ReopenCount++;
        }
        else
        {
            if (target.Value == "Resolved")
            {
                if (resolution is null)
                {
                    throw new InvalidOperationException(
                        $"Ticket '{Reference}' cannot be resolved without a resolution code and notes.");
                }

                var code = TicketResolutionCode.Create(resolution.Code);

                if (string.IsNullOrWhiteSpace(resolution.Notes))
                {
                    throw new ArgumentException("Resolution notes are required", nameof(resolution));
                }

                if (resolution.Notes.Length > 2000)
                {
                    throw new ArgumentException("Resolution notes must not exceed 2000 characters", nameof(resolution));
                }

                ResolutionCode = code.Value;
                ResolutionNotes = resolution.Notes.Trim();
                ResolvedAt = DateTime.UtcNow;
            }

            if (target.Value == "Closed") ClosedAt = DateTime.UtcNow;
        }

        Status = target.Value;
        MarkUpdated();
        UpdatedBy = actorId;

        ApplySlaPauseTransition(current.Value, target.Value);

        Append(actorId, changeType, current.Value, target.Value);
        AddDomainEvent(new TicketStatusChangedEvent(Id, Reference, current.Value, target.Value, actorId));
    }

    /// <summary>
    /// FEAT-17 second slice, AC-134..AC-136 (`BR-16`/`BR-17`). Entering <c>Pending</c> starts the
    /// pause; leaving it accumulates the elapsed span and shifts both due dates forward by the same
    /// span, so time spent waiting on the customer is not counted against the SLA.
    /// </summary>
    private void ApplySlaPauseTransition(string fromStatus, string toStatus)
    {
        // AC-504: both "Waiting for Customer" and "Waiting for Internal Team" pause the SLA.
        // Entering either starts the pause; leaving either (back to "In Progress") accumulates
        // the elapsed span and shifts both due dates forward by that span.
        bool isWaitingStatus(string s) =>
            s is "Waiting for Customer" or "Waiting for Internal Team";

        if (isWaitingStatus(toStatus) && PausedAt is null)
        {
            PausedAt = DateTime.UtcNow;
            return;
        }

        if (isWaitingStatus(fromStatus) && toStatus != fromStatus && PausedAt is { } pausedAt)
        {
            var elapsed = DateTime.UtcNow - pausedAt;
            // Persist whole seconds, but never lose a short pause completely.
            TotalPausedSeconds += Math.Max(1, (int)Math.Ceiling(elapsed.TotalSeconds));
            PausedAt = null;

            if (ResponseDueAt is { } responseDue)
            {
                ResponseDueAt = responseDue.Add(elapsed);
            }

            if (ResolutionDueAt is { } resolutionDue)
            {
                ResolutionDueAt = resolutionDue.Add(elapsed);
            }
        }
    }

    /// <summary>
    /// Raises the escalation level (FEAT-17 second slice, AC-138). Called only by the breach
    /// scanner — the "only escalate from `None`" rule (AC-139) is the caller's responsibility, since
    /// this method has no way to distinguish "already escalated, leave it" from a future slice
    /// legitimately forcing a level.
    /// </summary>
    public void Escalate(string level)
    {
        EscalationState = level;
        MarkUpdated();
    }

    /// <summary>
    /// Advances the escalation level — the US-218 progression path (AC-218.1..AC-218.3), distinct
    /// from the single-level <see cref="Escalate"/> used by the pre-progression AC-138 flow.
    ///
    /// The transition is guarded: it only applies when the ticket's current <c>EscalationState</c>
    /// still equals <paramref name="fromLevel"/> (so a stale cursor — a concurrent scan that already
    /// advanced the ticket, or a repeated pass — is refused, which is what AC-218.3's idempotency
    /// rests on), the move is genuinely forward (<paramref name="toLevel"/> differs from
    /// <paramref name="fromLevel"/>), and the actor is a real one. A refusal throws
    /// <see cref="InvalidOperationException"/>; the scanner treats that as "already applied / not a
    /// duplicate transition" rather than surfacing a 500.
    ///
    /// On success exactly one <c>Escalated</c> history row is appended recording the previous and
    /// next levels under the system actor (AC-48 append-only audit).
    /// </summary>
    public void AdvanceEscalation(string fromLevel, string toLevel, Guid systemActor)
    {
        if (systemActor == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(systemActor));
        }

        if (string.IsNullOrWhiteSpace(fromLevel))
        {
            throw new ArgumentException("A source level is required", nameof(fromLevel));
        }

        if (string.IsNullOrWhiteSpace(toLevel))
        {
            throw new ArgumentException("A target level is required", nameof(toLevel));
        }

        if (toLevel == fromLevel)
        {
            throw new InvalidOperationException(
                $"Escalation for ticket '{Reference}' must move to a higher level, not stay at '{fromLevel}'.");
        }

        if (EscalationState != fromLevel)
        {
            throw new InvalidOperationException(
                $"Escalation for ticket '{Reference}' is stale: expected '{fromLevel}' but the ticket is at '{EscalationState}'. " +
                "The transition was already applied or superseded.");
        }

        EscalationState = toLevel;
        MarkUpdated();
        UpdatedBy = systemActor;

        Append(systemActor, TicketChangeType.Escalated, fromLevel, toLevel);
    }

    /// <summary>
    /// Sets the assignee (AC-42). **Whether the caller may do this is not decided here** — that is
    /// a role check the handler makes (AC-43), because the aggregate does not know the caller's
    /// roles and should not be asked to.
    /// </summary>
    public void AssignTo(Guid assigneeId, Guid actorId)
    {
        if (assigneeId == Guid.Empty)
        {
            throw new ArgumentException("An assignee is required", nameof(assigneeId));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(actorId));
        }

        if (AssigneeId == assigneeId)
        {
            throw new InvalidOperationException(
                $"Ticket '{Reference}' is already assigned to that user.");
        }

        var previous = AssigneeId;
        var changeType = previous is null ? TicketChangeType.Assigned : TicketChangeType.Reassigned;

        AssigneeId = assigneeId;
        MarkUpdated();
        UpdatedBy = actorId;

        Append(actorId, changeType, previous?.ToString(), assigneeId.ToString());
        AddDomainEvent(new TicketAssignedEvent(Id, Reference, previous, assigneeId, actorId));
    }

    /// <summary>
    /// Whether this ticket belongs to the given user. The per-record half of AC-45 and AC-46: only
    /// the loaded ticket knows who holds it, which is why no endpoint policy can answer this.
    /// </summary>
    public bool IsAssignedTo(Guid userId) => AssigneeId is not null && AssigneeId == userId;

    /// <summary>
    /// Sets the SLA due dates computed at creation (AC-128). Not a general setter — nothing in this
    /// slice re-evaluates SLA on an already-created ticket, so this is only ever called from
    /// <see cref="Create"/>.
    /// </summary>
    public void SetSlaTargets(DateTime? responseDueAt, DateTime? resolutionDueAt)
    {
        ResponseDueAt = responseDueAt;
        ResolutionDueAt = resolutionDueAt;
    }

    /// <summary>
    /// Records the channel this ticket originated on. Set once, immediately after
    /// <see cref="Create"/>, by the inbound-channel ingestion handler — never on an agent-created
    /// ticket and never changed afterwards.
    /// </summary>
    public void SetSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("A source channel is required", nameof(source));
        }

        Source = source;
    }

    /// <summary>
    /// FEAT-21 / AC-21.5 — the one mutation path for an accepted AI category suggestion. Deliberately
    /// routed through the entity like every other category change, so a suggestion can never set a
    /// category id that does not exist or bypass validation.
    /// </summary>
    public void ApplySuggestedCategory(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("A category is required", nameof(categoryId));
        }

        CategoryId = categoryId;
        MarkUpdated();
    }

    /// <summary>Corrects the descriptive fields. The lifecycle fields are not reachable from here.</summary>
    public void UpdateDetails(string? subject, string? description, Guid actorId)
    {
        if (!string.IsNullOrWhiteSpace(subject))
        {
            if (subject.Length > 200)
            {
                throw new ArgumentException("Subject must not exceed 200 characters", nameof(subject));
            }

            Subject = subject.Trim();
        }

        if (description is not null)
        {
            Description = description;
        }

        MarkUpdated();
        UpdatedBy = actorId;
    }

    /// <summary>
    /// US-923 / AC-923.2. Sets the matrix inputs and re-derives priority — the only mutation path
    /// priority has (spec decision: matrix-only). A changed derivation is recorded; an unchanged
    /// one is not history, because nothing the queue sorts on moved.
    /// </summary>
    public void Reclassify(string impact, string urgency, Guid actorId)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(actorId));
        }

        var impactVo = TicketImpact.Create(impact);
        var urgencyVo = TicketUrgency.Create(urgency);
        var derived = PriorityMatrix.Derive(impactVo, urgencyVo).Value;

        Impact = impactVo.Value;
        Urgency = urgencyVo.Value;

        if (derived != Priority)
        {
            var previous = Priority;
            Priority = derived;
            Append(actorId, TicketChangeType.Reprioritized, previous, derived);
        }

        MarkUpdated();
        UpdatedBy = actorId;
    }

    /// <summary>
    /// AC-510/A5. Called on every outbound message; the first call sets both, later calls only move
    /// <see cref="LastResponseAt"/>. One stamp, two consumers.
    /// </summary>
    public void RecordResponse(DateTime stampedAt)
    {
        FirstResponseAt ??= stampedAt;
        LastResponseAt = stampedAt;
        MarkUpdated();
    }

    /// <summary>
    /// US-907 / AC-511. Populates the dormant organisational columns — from the assignee on assign,
    /// from the acting agent at creation (A7). Nulls mean "not wired", never a default.
    /// </summary>
    public void InheritOrganisation(Guid? departmentId, Guid? branchId, Guid? teamId)
    {
        DepartmentId = departmentId;
        BranchId = branchId;
        TeamId = teamId;
        MarkUpdated();
    }

    /// <summary>
    /// US-904 / AC-506. Hands the escalated ticket to a named owner, recording an <c>Escalated</c>
    /// history row per hand-off (append-only, AC-48). The escalation *level* is untouched — it is the
    /// scanner's field; this names who is doing the work.
    /// </summary>
    public void TakeEscalation(Guid specialistId, Guid actorId)
    {
        if (specialistId == Guid.Empty)
        {
            throw new ArgumentException("An escalation owner is required", nameof(specialistId));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(actorId));
        }

        if (EscalationState == "None")
        {
            throw new InvalidOperationException($"Ticket '{Reference}' is not escalated and has no owner to take.");
        }

        if (EscalationAssigneeId == specialistId)
        {
            throw new InvalidOperationException($"Ticket '{Reference}' is already held by that owner.");
        }

        var previous = EscalationAssigneeId;

        EscalationAssigneeId = specialistId;
        MarkUpdated();
        UpdatedBy = actorId;

        Append(actorId, TicketChangeType.Escalated, previous?.ToString(), specialistId.ToString());
    }

    private void Append(Guid actorId, TicketChangeType changeType, string? from, string? to)
    {
        _history.Add(TicketHistory.Record(Id, actorId, changeType, from, to));
    }
}
