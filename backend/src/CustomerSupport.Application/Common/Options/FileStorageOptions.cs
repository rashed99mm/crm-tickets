namespace CustomerSupport.Application.Common.Options;

/// <summary>
/// The storage root and the two limits every upload is measured against (A20).
///
/// <b>Why this sits in Application rather than beside <c>LocalFileStore</c> in Infrastructure,
/// where the plan sketched it.</b> The upload handler needs <see cref="MaxBytes"/> and
/// <see cref="AllowedContentTypes"/> to refuse a file <em>before</em> the stream is consumed, and a
/// handler cannot read a type that lives in Infrastructure without Application referencing
/// Infrastructure — which is the one dependency this codebase does not bend. So the policy lives
/// here with the code that enforces it, and Infrastructure binds it from configuration.
/// </summary>
public sealed class FileStorageOptions
{
    /// <summary>The configuration section this binds from.</summary>
    public const string SectionName = "FileStorage";

    /// <summary>
    /// The directory the bytes live in. **Outside the web root** (A18): a file under wwwroot is a
    /// public URL, and AC-26 requires a session for every download.
    /// </summary>
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "App_Data", "attachments");

    /// <summary>10 MB — A20, and the number AC-23 refuses above.</summary>
    public const long DefaultMaxBytes = 10 * 1024 * 1024;

    /// <summary>See <see cref="RequestBodyLimitBytes"/>. A constant because
    /// <c>[RequestSizeLimit]</c> is an attribute and can take nothing else.</summary>
    public const long DefaultRequestBodyLimitBytes = DefaultMaxBytes + (1024 * 1024);

    /// <inheritdoc cref="DefaultMaxBytes"/>
    public long MaxBytes { get; set; } = DefaultMaxBytes;

    /// <summary>
    /// AC-24, and an <b>allowlist</b>. A blocklist is a list of the attacks somebody already
    /// thought of; this refuses everything nobody has vouched for, including the next attack.
    /// </summary>
    public HashSet<string> AllowedContentTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "application/pdf",
        "text/plain",
    };

    /// <summary>
    /// The transport ceiling for an upload request, deliberately a little above
    /// <see cref="MaxBytes"/>. It has to leave room for the multipart envelope, and it has to be
    /// set at all so that Kestrel's 30 MB default is not what silently enforces AC-23 — a limit
    /// nobody chose is a limit nobody can change on purpose.
    ///
    /// It is a ceiling, not the rule: it stops a request nobody could serve, and the handler's own
    /// check against <see cref="MaxBytes"/> is what produces AC-23's envelope.
    /// </summary>
    public long RequestBodyLimitBytes => MaxBytes + (1024 * 1024);
}
