namespace CustomerSupport.Application.Features.Customers.Dtos;

/// <summary>
/// One interaction record — AC-74.
///
/// <see cref="AuthorName"/> is projected at read time; the row stores <see cref="AuthorId"/> only.
/// Writing the name into the row would freeze a value that changes and duplicate personal data into
/// a table nothing can correct, because notes are never edited (A13).
/// </summary>
public record CustomerNoteDto(
    Guid Id,
    string Body,
    Guid AuthorId,
    string AuthorName,
    DateTime CreatedAt);

/// <summary>
/// The create payload — AC-75.
///
/// Deliberately has <b>no</b> author field. AC-76 says the author comes from the session, and a
/// field that does not exist cannot be honoured by accident: any <c>authorId</c> a client sends
/// lands on nothing during model binding and never reaches the handler.
/// </summary>
public record CreateCustomerNoteRequest(string Body);
