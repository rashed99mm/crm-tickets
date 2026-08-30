using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Infrastructure.Notifications;

/// <summary>
/// Resolves a template per (TemplateCode, Channel) and substitutes {{Variable}} placeholders.
/// The platform has no template store wired yet, so this renders from the request Variables and
/// treats <c>Title</c>/<c>Message</c> as the fields; missing placeholders fail the dispatch.
/// </summary>
public sealed class NotificationTemplateRenderer : INotificationTemplateRenderer
{
    public Task<RenderedNotification> RenderAsync(
        NotificationDispatchRequest request,
        NotificationChannel channel,
        CancellationToken ct = default)
    {
        var variables = request.Variables ?? new Dictionary<string, string>();

        var titleTemplate = variables.TryGetValue("Title", out var t) ? t : request.TemplateCode;
        var messageTemplate = variables.TryGetValue("Message", out var m) ? m : request.TemplateCode;

        var title = Render(titleTemplate, variables);
        var message = Render(messageTemplate, variables);

        return Task.FromResult(new RenderedNotification(
            request.RecipientUserId,
            request.Email,
            request.PhoneNumber,
            title,
            message,
            request.TemplateCode,
            channel,
            Locale: null));
    }

    private static string Render(string template, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template))
            return template ?? string.Empty;

        return System.Text.RegularExpressions.Regex.Replace(
            template,
            @"\{\{\s*(?<key>[A-Za-z0-9_]+)\s*\}\}",
            match =>
            {
                var key = match.Groups["key"].Value;
                if (variables.TryGetValue(key, out var value))
                    return value;
                throw new InvalidOperationException(
                    $"Notification template variable '{key}' is not provided. [{ApplicationErrors.Notification.TEMPLATE_INVALID}]");
            });
    }
}
