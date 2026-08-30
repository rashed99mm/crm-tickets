using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.Interfaces;

namespace CustomerSupport.Infrastructure.Storage;

/// <summary>
/// <see cref="IFileStore"/> over the local filesystem, rooted at
/// <see cref="FileStorageOptions.RootPath"/> (A18).
///
/// The root is created on construction rather than on first write, so a misconfigured path fails
/// at start-up where somebody is looking, instead of on the first upload of the day.
/// </summary>
public sealed class LocalFileStore : IFileStore
{
    private readonly string _root;

    public LocalFileStore(FileStorageOptions options)
    {
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.RootPath));
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string storedFileName, Stream content, CancellationToken ct = default)
    {
        var path = Resolve(storedFileName);

        // CreateNew, not Create. The stored name is a fresh GUID and UX_Assets_StoredFileName says
        // so at the database too, so an existing file here means an assumption has broken — and
        // silently overwriting somebody else's bytes is the worst available response to that.
        await using var destination = new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);

        await content.CopyToAsync(destination, ct);
    }

    public Task<Stream?> OpenAsync(string storedFileName, CancellationToken ct = default)
    {
        var path = Resolve(storedFileName);

        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storedFileName, CancellationToken ct = default)
    {
        var path = Resolve(storedFileName);

        // Idempotent. A caller deleting a row whose bytes are already gone is finishing a job that
        // was half done, not committing an error.
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves a stored name to an absolute path, and asserts containment on <b>every</b> call —
    /// opens and deletes included, not just writes.
    ///
    /// <c>Asset.Create</c> already generates the stored name from a GUID, so in principle nothing
    /// hostile ever reaches here. This is the second lock. A defence that depends only on name
    /// generation staying correct is one refactor away from a traversal, and the cost of being sure
    /// is a <see cref="Path.GetFullPath(string)"/>. Reading and deleting need it as much as writing
    /// does: <c>../../appsettings.json</c> is a disclosure through <see cref="OpenAsync"/> and a
    /// destruction through <see cref="DeleteAsync"/>.
    /// </summary>
    private string Resolve(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
        {
            throw new InvalidOperationException("A stored file name is required.");
        }

        var full = Path.GetFullPath(Path.Combine(_root, storedFileName));

        // The trailing separator matters: without it "/data/attachments-public" would pass as a
        // prefix match against a root of "/data/attachments".
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resolved path escapes the storage root: '{storedFileName}'.");
        }

        // A name that resolved inside the root but named a subdirectory would still be a name the
        // domain did not generate. Files sit flat under the root; nothing else is expected.
        if (Path.GetDirectoryName(full) != _root)
        {
            throw new InvalidOperationException(
                $"Stored files live directly under the root: '{storedFileName}'.");
        }

        return full;
    }
}
