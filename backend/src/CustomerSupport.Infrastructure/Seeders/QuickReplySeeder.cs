using CustomerSupport.Domain.Entities.Support;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Seeders;

public class QuickReplySeeder(AppDbContext db)
{
    public static readonly (string Shortcut, string Body)[] Seeds =
    [
        ("HI", "Hello! Thank you for reaching out. How can I assist you today?"),
        ("THX", "Thank you for your patience. We appreciate your business."),
        ("WRKG", "We are looking into this for you. Please allow us some time."),
        ("RES", "This issue has been resolved. Is there anything else I can help you with?"),
        ("CLS", "Thank you for contacting us. Have a great day!"),
        ("ESC", "I understand your concern and I am escalating this to our specialist team. You will hear back within 24 hours."),
        ("INFO", "Could you please provide more details so we can assist you better?"),
        ("RFSH", "Have you tried clearing your cache and cookies? This often resolves the issue."),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existing = await db.QuickReplies
            .IgnoreQueryFilters()
            .Select(q => q.Shortcut)
            .ToListAsync(ct);

        var missing = Seeds.Where(s => !existing.Contains(s.Shortcut)).ToList();
        if (missing.Count == 0)
            return;

        foreach (var (shortcut, body) in missing)
        {
            db.QuickReplies.Add(QuickReply.Create(shortcut, body));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            foreach (var entry in db.ChangeTracker.Entries<QuickReply>().ToList())
                entry.State = EntityState.Detached;

            if ((await db.QuickReplies.IgnoreQueryFilters().Select(q => q.Shortcut).ToListAsync(ct)).Count < Seeds.Length)
                throw;
        }
    }
}
