using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Entities.Survey;

/// <summary>
/// One customer satisfaction response against a resolved ticket (US-408/US-409).
///
/// A ticket is surveyable once, so <c>UX_SurveyResponses_TicketId</c> is a unique index and the
/// submit handler checks it first (PJ-11). Append-only like a history row: a response, once
/// written, is the record of the customer's experience and must not be edited away (ADR-0010).
/// </summary>
public class SurveyResponse : BaseEntity, IAppendOnlyEntity
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxFreeTextLength = 2000;

    public Guid TicketId { get; private set; }
    public int Rating { get; private set; }
    public string? FreeText { get; private set; }

    public static SurveyResponse Create(Guid ticketId, int rating, string? freeText)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A ticket is required", nameof(ticketId));
        }

        if (rating is < MinRating or > MaxRating)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), $"Rating must be between {MinRating} and {MaxRating}");
        }

        var trimmed = string.IsNullOrWhiteSpace(freeText) ? null : freeText.Trim();

        if (trimmed is { Length: > MaxFreeTextLength })
        {
            throw new ArgumentException($"Free text must not exceed {MaxFreeTextLength} characters", nameof(freeText));
        }

        return new SurveyResponse
        {
            // Id deliberately unassigned — see TicketMessage.Create for why: a client-assigned Guid
            // on a row appended to an already-tracked Ticket makes EF mark it Modified, and the
            // append-only guard then refuses a perfectly legitimate append.
            TicketId = ticketId,
            Rating = rating,
            FreeText = trimmed,
            CreatedAt = DateTime.UtcNow
        };
    }
}