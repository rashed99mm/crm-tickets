namespace CustomerSupport.Application.Features.Tickets.Dtos;

/// <summary>
/// One ticket attachment as a list/query returns it (TA-4/TA-10). Mirrors
/// <c>CustomerAttachmentDto</c>; <see cref="Id"/> is the link id, which is what the download route
/// addresses. The asset's <c>StoredFileName</c> is deliberately absent for the same reason it is
/// absent on the customer DTO: it is the name on disk and publishing it would hand out the one string
/// a traversal would want.
/// </summary>
public record TicketAttachmentDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedById,
    string UploadedByName,
    DateTime CreatedAt);
