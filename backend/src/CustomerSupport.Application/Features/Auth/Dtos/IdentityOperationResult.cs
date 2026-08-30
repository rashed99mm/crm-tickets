namespace CustomerSupport.Application.Features.Auth.Dtos;

public sealed record IdentityOperationResult(
    bool Succeeded,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> ErrorCodes)
{
    public static IdentityOperationResult Success() =>
        new(true, Array.Empty<string>(), Array.Empty<string>());

    public static IdentityOperationResult Failure(IEnumerable<string> errors) =>
        new(false, errors.ToArray(), Array.Empty<string>());

    /// <summary>
    /// Preserves each <c>IdentityError</c>'s stable <c>Code</c> alongside its human-readable
    /// description, so a caller can branch on the failure reason (e.g. distinguish a wrong
    /// current password from a weak new one) without parsing prose.
    /// </summary>
    public static IdentityOperationResult Failure(
        IEnumerable<(string Code, string Description)> errors)
    {
        var materialized = errors.ToArray();
        return new(
            false,
            materialized.Select(e => e.Description).ToArray(),
            materialized.Select(e => e.Code).ToArray());
    }
}
