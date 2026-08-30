# MVP-06 — Customer attachments · **backend** implementation plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Date:** 2026-08-26
**Spec:** [`../../specs/EPIC-02-US-001-customer-attachments.md`](../../specs/EPIC-02-US-001-customer-attachments.md)
**Criteria:** `AC-22`…`AC-28` (approved)
**Independent of the frontend plan** — the screen shape does not affect any of this.

## What already exists

`Asset` (with `Asset.Create` generating a GUID stored name from the original's extension),
`CustomerAttachment` (the ownership link), both tables, `UX_Assets_StoredFileName`,
`UX_CustomerAttachments_Asset`, and the passing unit test
`AC25_The_Stored_Name_Is_Server_Generated_And_Cannot_Escape_The_Directory`.

**No migration. No entity work.** This is the port, its implementation, four endpoints and the tests.

## Code plan

### T1 — The storage port

**New:** `src/CustomerSupport.Application/Interfaces/IFileStore.cs`

```csharp
/// Bytes live outside the database. Declared here and implemented in Infrastructure so a handler
/// never learns whether storage is a disk, a share or a bucket (A18).
public interface IFileStore
{
    Task SaveAsync(string storedFileName, Stream content, CancellationToken ct = default);
    Task<Stream?> OpenAsync(string storedFileName, CancellationToken ct = default);
    Task DeleteAsync(string storedFileName, CancellationToken ct = default);
}
```

### T2 — The filesystem implementation, and the containment assertion

**New:** `src/CustomerSupport.Infrastructure/Storage/LocalFileStore.cs` +
`FileStorageOptions` (`RootPath`, bound from configuration, **outside the web root**).

```csharp
/// Resolves and asserts containment on EVERY call, not only on save.
///
/// Asset.Create already generates the stored name, so in principle nothing hostile reaches here.
/// This is the second lock: a defence that depends only on name generation staying correct is one
/// refactor away from a traversal, and the cost of checking is a Path.GetFullPath.
private string Resolve(string storedFileName)
{
    var root = Path.GetFullPath(options.RootPath);
    var full = Path.GetFullPath(Path.Combine(root, storedFileName));

    if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        throw new InvalidOperationException(
            $"Resolved path escapes the storage root: '{storedFileName}'.");

    return full;
}
```

Register in `ServiceCollectionExtensions`; create the root directory on start if absent.

### T3 — Upload · `AC-22`, `AC-23`, `AC-24`, `AC-27`

**New:** `.../Features/Customers/Commands/AddCustomerAttachment/AddCustomerAttachmentCommand.cs`

**The order of checks is the design.** Each must refuse before the next cost is incurred:

```csharp
// 1. Customer exists (AC-27) — cheapest, and a 404 makes everything else moot.
// 2. Size (AC-23). Checked from the declared length BEFORE the stream is read. The criterion says
//    "nothing is written to disk"; a check after buffering has already failed it.
// 3. Content type (AC-24), against an ALLOWLIST — a blocklist is a list of the attacks someone
//    already thought of.
// 4. Only now: Asset.Create, IFileStore.SaveAsync, link row, SaveChanges.
```

If `SaveChangesAsync` throws after the bytes land, **delete the file** before returning — otherwise
the disk accumulates orphans no row references.

Limits live in `FileStorageOptions`: `MaxBytes = 10 * 1024 * 1024`, and the allowlist from `A20`.

Status codes: `AC-23` → **413** and `AC-24` → **415**. `ErrorType` has no member for either, so
`MapFailureStatusCode` in `ResultActionResultExtensions` needs two new arms —
`ErrorType.PayloadTooLarge` and `ErrorType.UnsupportedMediaType` added to
`Domain/Common/Error.cs`. **This is a change to a shared file; keep it additive.**

Also raise the endpoint's request-body limit deliberately
(`[RequestSizeLimit]`) so Kestrel's own 30 MB default is not what silently enforces the rule.

### T4 — List, download, delete · `AC-26`, `AC-28`

- `GetCustomerAttachmentsQuery` — paged, joins `CustomerAttachments` to `Assets`, newest first,
  projecting id, original filename, content type, size, uploader name, when.
- `DownloadCustomerAttachmentQuery` — returns the stream plus content type and original filename;
  the controller answers `File(stream, contentType, originalFileName)` so
  `Content-Disposition` carries a name a human recognises. **Requires a session** — the whole point
  of streaming rather than serving from a static path.
- `RemoveCustomerAttachmentCommand` — soft-delete the link, soft-delete the asset, then
  `IFileStore.DeleteAsync`. **Row first, file second**: a deleted file with a live row is a broken
  download, a live file with a deleted row is invisible and reclaimable.

### T5 — Endpoints

**Edit:** `CustomersController` — four actions under `{id:guid}/attachments`, XML-documented.
Upload takes `IFormFile`.

### T6 — Codes and messages

`ATTACHMENT_ADDED`, `ATTACHMENT_NOT_FOUND`, `ATTACHMENT_TOO_LARGE`, `ATTACHMENT_TYPE_NOT_ALLOWED`,
`ATTACHMENT_REMOVED` — each with an `ar`/`en` pair in `Resources.yaml`, or
`EveryErrorCode_HasABilingualMessage` fails.

## Tests — `AC-23`, `AC-24` and `AC-25` must assert the filesystem, not the status code

**New:** `tests/CustomerSupport.Tests/Integration/CustomerAttachmentEndpointTests.cs`, pointing
`FileStorage:RootPath` at a per-run temp directory so the disk can be inspected.

| Test | Criterion |
|---|---|
| `AC22_Upload_PermittedFile_Returns201WithStoredMetadata` | `AC-22` |
| `AC23_Upload_OverTheSizeLimit_Returns413AndWritesNothingToDisk` | `AC-23` |
| `AC24_Upload_TypeOutsideTheAllowlist_Returns415AndWritesNothingToDisk` | `AC-24` |
| `AC25_Upload_HostileFilename_StoresAGuidInsideTheRoot` | `AC-25` |
| `AC26_Download_ReturnsTheContentTypeAndOriginalFilename` | `AC-26` |
| `AC26_Download_WithoutAToken_Returns401` | `AC-26` |
| `AC27_Upload_UnknownCustomer_Returns404` | `AC-27` |
| `AC28_Remove_DeletesTheRowAndTheFile` | `AC-28` |

The three "writes nothing" tests must **count files in the storage root before and after** and assert
the count is unchanged. A handler that writes then deletes passes a status-only assertion and fails
the criterion as written.

`AC25_…` uploads `../../etc/passwd` and `..\..\windows\system32\config\sam`, then asserts exactly one
new file exists, its name parses as a GUID, and it sits directly under the root.

## Definition of done

`AC-22`…`AC-28` each covered by a test naming it · `dotnet test` green with output pasted ·
0 errors, no new warnings · task records in `tasks/`. **Do not touch `frontend/`.**
