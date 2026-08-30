namespace CustomerSupport.Application.Features.Customers.Dtos;

/// <summary>
/// One attachment as the list returns it — AC-22, AC-26.
///
/// <see cref="Id"/> is the <b>link</b> id, not the asset id: it is what the download and delete
/// routes address, and it is the identifier that belongs to this customer. The asset id is a
/// catalogue detail the API has no reason to publish.
///
/// The asset's <c>StoredFileName</c> is deliberately absent — <c>c</c>, not a <c>cref</c>, precisely
/// because it is not a member here. It is the name on disk, it is of no use to
/// a client, and publishing it would hand out the one string a traversal would want.
/// </summary>
public record CustomerAttachmentDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedById,
    string UploadedByName,
    DateTime CreatedAt);

/// <summary>
/// The bytes plus what a browser needs to name and interpret them — AC-26.
///
/// Carries an open <see cref="Stream"/>, so the caller owns disposing it. That is the point of
/// streaming rather than serving a static path: the file never has a URL of its own, so the session
/// check cannot be bypassed by knowing where it lives.
/// </summary>
public record AttachmentContentDto(Stream Content, string ContentType, string OriginalFileName);
