namespace CustomerSupport.Domain.Entities.Assets;

/// <summary>
/// The catalogue row for one stored file — the single point of entry for every file the product
/// stores. The bytes live outside the database behind <c>IFileStore</c>.
/// </summary>
public class Asset : BaseEntity
{
    /// <summary>What the uploader called it. Metadata only; it never reaches the filesystem.</summary>
    public string OriginalFileName { get; private set; } = string.Empty;

    /// <summary>
    /// The name on disk: a fresh GUID plus the original extension. Generated here rather than
    /// derived from user input, which is what makes a filename containing <c>../</c> harmless
    /// (AC-25) instead of something a sanitiser has to be trusted to catch.
    /// </summary>
    public string StoredFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public Guid UploadedById { get; private set; }

    /// <summary>
    /// The size cap (AC-23) and the content-type allowlist (AC-24) are enforced before the stream
    /// is consumed, which is a handler's job — by the time an Asset exists the bytes have been
    /// read, and a check here would be too late to prevent the write it is meant to prevent.
    /// </summary>
    public static Asset Create(string originalFileName, string contentType, long sizeBytes, Guid uploadedById)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException("A file name is required", nameof(originalFileName));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("A content type is required", nameof(contentType));
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentException("A file must have content", nameof(sizeBytes));
        }

        if (uploadedById == Guid.Empty)
        {
            throw new ArgumentException("An uploader is required", nameof(uploadedById));
        }

        return new Asset
        {
            Id = Guid.NewGuid(),
            OriginalFileName = originalFileName.Trim(),
            StoredFileName = GenerateStoredName(originalFileName),
            ContentType = contentType.Trim(),
            SizeBytes = sizeBytes,
            UploadedById = uploadedById,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = uploadedById
        };
    }

    /// <summary>
    /// Takes the extension and nothing else. <c>Path.GetExtension</c> on a hostile name yields at
    /// worst a string with no separators in it, and anything that still looks like a path segment
    /// is dropped rather than cleaned.
    /// </summary>
    private static string GenerateStoredName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);

        if (string.IsNullOrEmpty(extension)
            || extension.Length > 16
            || extension.IndexOfAny([.. Path.GetInvalidFileNameChars()]) >= 0)
        {
            extension = string.Empty;
        }

        return $"{Guid.NewGuid():N}{extension}";
    }
}
