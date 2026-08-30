using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Seeders;

/// <summary>Idempotent starter content for the customer-facing knowledge base.</summary>
public sealed class ContentSeeder(AppDbContext db, UserManager<ApplicationUser> users)
{
    private sealed record SeedContent(string Title, string Summary, string Body, string Type, string Category, string[] Tags, string Image, bool IsFaq);

    private static readonly SeedContent[] Seeds =
    [
        new("How do I reset my password?", "Recover access securely in a few minutes.", "Open the sign-in page and select Forgot password. Follow the verification link sent to your registered email, then choose a strong new password. If the email does not arrive, check your spam folder or contact the support team.", "FAQ", "Account & Billing", ["password", "account", "security"], "https://images.unsplash.com/photo-1555949963-ff9fe0c870eb?auto=format&fit=crop&w=1200&q=80", true),
        new("How can I track my ticket?", "See status, responses, and SLA updates from your workspace.", "Open My tickets from the portal navigation and select a ticket to view its current status, conversation history, attachments, and the latest support response.", "FAQ", "Getting Started", ["tickets", "portal", "status"], "https://images.unsplash.com/photo-1553877522-43269d4ea984?auto=format&fit=crop&w=1200&q=80", true),
        new("Building a reliable support workflow", "A practical guide to routing, prioritising, and resolving customer requests.", "A reliable workflow starts with a clear intake form, a consistent priority policy, and an owner for every active request. Use categories to route work, keep internal notes separate from customer messages, and review SLA signals before a breach occurs.", "Article", "Getting Started", ["workflow", "automation", "support"], "https://images.unsplash.com/photo-1556761175-b413da4baf72?auto=format&fit=crop&w=1200&q=80", false),
        new("Automation playbook for SLA protection", "Use signals and escalation rules to keep urgent work visible.", "Configure response and resolution targets for each priority. Warning signals should notify the assigned agent first, then escalate unresolved work to the supervisor. Keep every transition in the ticket history so teams can audit the decision and improve the policy.", "Guide", "Troubleshooting", ["sla", "automation", "escalation"], "https://images.unsplash.com/photo-1517245386807-bb43f82c33c4?auto=format&fit=crop&w=1200&q=80", false),
        new("Connecting external channels safely", "Bring email, chat, and partner events into one support record.", "Normalize inbound channel messages before creating or updating a ticket. Validate signatures, apply idempotency keys, and retain the original channel metadata so agents can reply through the correct delivery route.", "Article", "Integrations", ["integrations", "channels", "security"], "https://images.unsplash.com/photo-1558494949-ef010cbdcc31?auto=format&fit=crop&w=1200&q=80", false),
        new("Knowledge base governance guide", "Keep customer-facing content accurate, searchable, and safe to publish.", "Assign an owner to every article, use a short summary for search results, and review guides after major workflow changes. Publish only reviewed content and archive outdated instructions instead of silently changing the history.", "Guide", "Security & Privacy", ["knowledge-base", "governance", "content"], "https://images.unsplash.com/photo-1499750310107-5fef28a66643?auto=format&fit=crop&w=1200&q=80", false),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var author = await users.FindByEmailAsync("superadmin@support.local");
        if (author is null) return;

        var categories = await db.ContentCategories
            .Where(c => c.ParentId == null)
            .ToDictionaryAsync(c => c.Name, c => c.Id, ct);
        var contents = db.Set<Content>();
        var existing = await contents.IgnoreQueryFilters().Select(c => c.Title).ToListAsync(ct);

        foreach (var seed in Seeds.Where(s => !existing.Contains(s.Title, StringComparer.Ordinal)))
        {
            var content = Content.Create(seed.Title, seed.Body, seed.Type, author.Id, seed.Summary, seed.Category);
            content.UpdateFeaturedImage(seed.Image);
            content.UpdateTags(seed.Tags);
            content.UpdateIsFeatured(seed.IsFaq);
            if (categories.TryGetValue(seed.Category, out var categoryId)) content.AssignCategory(categoryId);
            content.Publish();
            if (seed.IsFaq) content.MarkAsFaq();
            contents.Add(content);
        }

        await db.SaveChangesAsync(ct);
    }
}
