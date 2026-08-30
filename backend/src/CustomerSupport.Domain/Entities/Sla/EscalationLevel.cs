namespace CustomerSupport.Domain.Entities.Sla;

/// <summary>
/// A configured step in the escalation ladder (US-218, AC-218.1/AC-218.2). Each row names a level
/// (e.g. <c>Level1</c>) and how many minutes of being breached must pass before a ticket advances
/// to that level, plus an optional role the escalation is aimed at.
///
/// Seeded, not admin-configured this pass (spec addendum A9) — an idempotent seeder writes the
/// defaults and no endpoint touches them, so no path here is reachable from a request payload.
/// </summary>
public class EscalationLevel : BaseEntity
{
    /// <summary>The level name, e.g. <c>Level1</c> / <c>Level2</c>. Locally unique (see configuration).</summary>
    public string Level { get; private set; } = string.Empty;

    /// <summary>Minutes of breach exposure before this level applies. Must be positive.</summary>
    public int BreachMinutes { get; private set; }

    /// <summary>The role the escalation is aimed at, or null when none is configured.</summary>
    public string? TargetRole { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Creates a level. Validates the name and positivity on creation so a bad level can never
    /// reach the table regardless of which path populates it.
    /// </summary>
    public static EscalationLevel Create(string level, int breachMinutes, string? targetRole, Guid id)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            throw new ArgumentException("Level is required", nameof(level));
        }

        if (breachMinutes <= 0)
        {
            throw new ArgumentException("BreachMinutes must be positive", nameof(breachMinutes));
        }

        return new EscalationLevel
        {
            Id = id,
            Level = level.Trim(),
            BreachMinutes = breachMinutes,
            TargetRole = string.IsNullOrWhiteSpace(targetRole) ? null : targetRole.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Returns the first active level strictly above <paramref name="currentLevel"/> in an
    /// ascending-ordered ladder, or <c>null</c> when there is no higher configured level (i.e. the
    /// current one is terminal). This is the pure selection rule (AC-218.2 terminal-by-absence-of-
    /// higher-level); callers supply the ordered set and this stays free of any persistence.
    /// </summary>
    public static EscalationLevel? NextFrom(IReadOnlyList<EscalationLevel> ordered, string currentLevel)
    {
        var current = Rank(currentLevel);

        foreach (var candidate in ordered)
        {
            if (!candidate.IsActive)
            {
                continue;
            }

            if (Rank(candidate.Level) > current)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Numeric rank of a level from a <c>Level&lt;n&gt;</c> name; anything unparseable ranks below
    /// every well-formed level so it can never block a real advancement.
    /// </summary>
    private static int Rank(string level)
    {
        if (level.StartsWith("Level", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(level.AsSpan(5), out var n))
        {
            return n;
        }

        return 0;
    }
}
