using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities;
using CustomerSupport.Domain.Entities.Assets;
using CustomerSupport.Domain.Entities.Audit;
using CustomerSupport.Domain.Entities.Channels;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Entities.Survey;
using CustomerSupport.Domain.Entities.Support;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Entities.Verification;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CustomerSupport.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    /// <summary>The sequence behind <c>TKT-nnnnnn</c> references. See ITicketReferenceGenerator.</summary>
    public const string TicketReferenceSequenceName = "TicketReferenceSequence";

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();
    public DbSet<CustomerAttachment> CustomerAttachments => Set<CustomerAttachment>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ContentCategory> ContentCategories => Set<ContentCategory>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<SLAPolicy> SLAPolicies => Set<SLAPolicy>();
    public DbSet<SLAEvent> SLAEvents => Set<SLAEvent>();
    public DbSet<EscalationLevel> EscalationLevels => Set<EscalationLevel>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketHistory> TicketHistory => Set<TicketHistory>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<LiveChatSession> LiveChatSessions => Set<LiveChatSession>();
    public DbSet<LiveChatMessage> LiveChatMessages => Set<LiveChatMessage>();
    public DbSet<Domain.Entities.Ai.AiSuggestion> AiSuggestions => Set<Domain.Entities.Ai.AiSuggestion>();
    public DbSet<Domain.Entities.Ai.AiChatSession> AiChatSessions => Set<Domain.Entities.Ai.AiChatSession>();
    public DbSet<Domain.Entities.Ai.AiChatMessage> AiChatMessages => Set<Domain.Entities.Ai.AiChatMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();
    public DbSet<SurveyResponse> SurveyResponses => Set<SurveyResponse>();
    public DbSet<TicketTask> TicketTasks => Set<TicketTask>();
    public DbSet<TicketNote> TicketNotes => Set<TicketNote>();
    public DbSet<QuickReply> QuickReplies => Set<QuickReply>();
    public DbSet<TicketTag> TicketTags => Set<TicketTag>();
    public DbSet<TicketLink> TicketLinks => Set<TicketLink>();

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        GuardAppendOnlyHistory();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added) { entry.Entity.CreatedAt = DateTime.UtcNow; }
            if (entry.State == EntityState.Modified) { entry.Entity.UpdatedAt = DateTime.UtcNow; }
            if (entry.State == EntityState.Deleted) { entry.Entity.IsDeleted = true; entry.Entity.DeletedAt = DateTime.UtcNow; entry.State = EntityState.Modified; }
        }

        return await base.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Any <see cref="IAppendOnlyEntity"/> row is append-only (AC-49). Enforced here rather than by
    /// absent columns, because the generic repository is constrained to <see cref="BaseEntity"/> —
    /// see ADR-0010, which also records what that trade costs. Generalised beyond
    /// <c>TicketHistory</c> so a second append-only entity (<c>TicketMessage</c>, FEAT-14) needs no
    /// change here at all — it only implements the interface.
    ///
    /// Runs before the audit pass below, which rewrites <c>Deleted</c> into <c>Modified</c>: both a
    /// hard delete and a soft one have to be refused, and each has to be refused by name.
    /// </summary>
    private void GuardAppendOnlyHistory()
    {
        foreach (var entry in ChangeTracker.Entries<IAppendOnlyEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} is append-only: row (Id {((BaseEntity)entry.Entity).Id}) " +
                    $"was {entry.State.ToString().ToLowerInvariant()} in this unit of work. " +
                    "Append a new row instead of altering the record of what happened.");
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplySoftDeleteQueryFilters(modelBuilder);

        // A sequence, not MAX(Reference) + 1: the latter races under concurrent inserts and the
        // unique index would turn that race into a 500.
        modelBuilder.HasSequence<long>(TicketReferenceSequenceName)
            .StartsAt(1000)
            .IncrementsBy(1);

        base.OnModelCreating(modelBuilder);
    }

    private static void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var isDeletedProperty = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
            var filter = Expression.Lambda(Expression.Equal(isDeletedProperty, Expression.Constant(false)), parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}
