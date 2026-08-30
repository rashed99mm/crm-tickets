# Portal Ticket Attachments — design spec

**Status:** Approved (2026-08-29) — scope extended to both portal and staff surfaces per approval
**Date:** 2026-08-29
**Feature:** Close the revealed dead-UI gap in the customer portal "Submit tickets" form
**Stories:** Customer portal submit tickets (US-059/US-404), ticket detail (US-406)
**Criterion trace:** FR-1.15 (retrieve an attachment), FR-1.16 (remove), SC-15 port of AC-22..AC-28 applied to tickets

## Problem

The customer portal "Submit tickets" form (`frontend/projects/portal-app/src/app/features/tickets/submit.component.html:114-126`)
renders an "Attachments" dropzone, but nothing in `submit.component.ts` selects, previews, uploads or
removes a file. The form posts JSON only (`PortalApi.submitTicket` → `POST /api/portal/tickets`),
and the backend endpoint (`PortalController.CreateTicket`) accepts JSON with no file field. There is
**no `TicketAttachment` concept anywhere** — the only file path is the staff-side customer
attachment rail (`/api/Customers/{id}/attachments`). So the dropzone is dead UI that promises a
feature which does not exist.

This spec closes that gap end-to-end by adapting the existing, proven `Asset` + link-entity upload
pattern (`EPIC-02-US-001-customer-attachments.md`) to tickets, on the portal host.

## Adopted baseline (reuse, not new machinery)

The platform already has a robust upload pipeline that this feature reuses unchanged:

- `Domain/Entities/Assets/Asset.cs` — the single file catalogue (original name, GUID stored name,
  content type, size, uploader).
- `Application/Common/Options/FileStorageOptions.cs` — 10 MB cap, content-type allowlist
  (`image/png|jpeg|gif`, `application/pdf`, `text/plain`).
- `Infrastructure/Storage/LocalFileStore.cs` — bytes outside web root, GUID-named, path-containment
  enforced on every read/write/delete. Registered on **both** hosts via
  `ServiceCollectionExtensions` (line 133-136) inside `AddPlatformInfrastructureServices`, which the
  ExternalApi host calls.
- `IRepository<T>` is generic (`AddScoped(typeof(IRepository<>))`, line 32), so `IRepository<Asset>`
  and `IRepository<TicketAttachment>` resolve on the portal host exactly as they do on the staff host.
- `AddCustomerAttachmentCommandHandler` (`.../Customers/Commands/AddCustomerAttachment/`) is the
  reference implementation to mirror for size/type refusal, stream handling and orphan cleanup.

The generic `IRepository<T>` scans and adds new entities to EF automatically only if they are
reachable from the `AppDbContext` (DbSets / model configuration). A new entity needs its EF model
mapping and a migration.

## Design

### 1. Domain — `TicketAttachment` link entity

New `Domain/Entities/Tickets/TicketAttachment.cs`, mirroring `CustomerAttachment` exactly: a thin
ownership link between a ticket and an `Asset`. Carries no file metadata (that lives in `Asset`).

```csharp
public class TicketAttachment : BaseEntity
{
    public Guid TicketId { get; private set; }
    public Guid AssetId { get; private set; }
    public static TicketAttachment Create(Guid ticketId, Guid assetId) { ... }
}
```

### 2. Application — `AddTicketAttachment` command + handler

New `Application/Features/Tickets/Commands/AddTicketAttachment/` mirroring the customer attachment
handler. Differences (portal surfaces must enforce ownership):

- Takes `TicketId, FileName, ContentType, DeclaredLength, Stream Content`.
- Verifies the ticket exists **and belongs to the requesting customer** (the handler enforces the
  same customer-scoping the other portal commands apply via `RequireCustomerId`) — otherwise 404
  (a ticket the customer does not own is "not found" to them).
- Empty → validation error; over `MaxBytes` → payload-too-large; non-allowlisted type →
  unsupported media. Same three refusals, same order, as the customer handler.
- `Asset.Create`, `TicketAttachment.Create`, `fileStore.SaveAsync`, then persist both rows and
  `SaveChangesAsync`; on any persistence failure, delete the orphaned file (mirroring the catch
  block with the logger).
- Portal downloads must also be ownership-scoped: `DownloadPortalTicketAttachmentQuery` verifies
  the ticket belongs to the customer before opening the stream.

### 3. API — ExternalApi `PortalController`

New actions mirroring the staff customer-attachment routes:

- `POST api/portal/tickets/{id:guid}/attachments` — `[Consumes("multipart/form-data")]`, `IFormFile? file`,
  `[RequestSizeLimit(FileStorageOptions.DefaultRequestBodyLimitBytes)]`. Opens the stream, dispatches
  `AddTicketAttachmentCommand`, returns the same envelope types (201/400/404/413/415).
- `GET api/portal/tickets/{id:guid}/attachments` — ownership-scoped list of attachments (id, name,
  content type, size) so the portal can render existing images (US-406).
- `GET api/portal/tickets/{id:guid}/attachments/{attachmentId:guid}/content` — streams bytes with
  the original filename (a session is required; never a static public URL, mirroring AC-26).

### 3b. API — InternalApi `TicketsController` (staff surface)

The same three actions on the staff host so agents can see (and add to) the images a customer
uploaded, reusing the same command/query. No customer-ownership scoping here — a staff member with
ticket access may read/write any ticket's attachments:

- `POST api/Tickets/{id:guid}/attachments` — multipart upload via `AddTicketAttachmentCommand`
  (ticket must exist; 404 otherwise; same size/type refusals).
- `GET api/Tickets/{id:guid}/attachments` — list of attachments.
- `GET api/Tickets/{id:guid}/attachments/{attachmentId:guid}/content` — stream with original filename.

### 4. EF model + migration

Add the `TicketAttachment` entity to the EF model (foreign key to `Ticket`, owned asset id, unique
stored-name guarantee comes from `Asset`), and add a migration creating the `TicketAttachments`
table + the FK to `Asset`. The generic repository picks it up from there. Mirror
`CustomerAttachmentConfiguration` in `AssetConfiguration.cs`.

### 5. Frontend — portal submit form + detail

**`submit.component.ts` / `.html` — make the dropzone real (tests later per workflow):**
- A hidden `<input type="file" multiple accept="image/png,image/jpeg,image/gif,application/pdf,text/plain">`
  driven by the dropzone, resetting `input.value=''` after each selection so the same file can be
  re-picked.
- Client-side pre-validation mirroring `validateImage`/size + allowlist: refuse too-large (10 MB)
  and non-allowlisted types with a per-file error message before anything reaches the network
  (mirror `customer-attachments.component.ts:171-191`).
- Selected-files state (`signal<readonly PendingAttachment[]>`) with image thumbnails via
  `URL.createObjectURL(file)` for `image/*` and a generic icon otherwise; per-file remove button.
- **Submit flow:** on `submitTicket` success (ticket created → returns `{ id }`), upload each
  pending file to `POST /api/portal/tickets/{id}/attachments` sequentially (bounded), showing a
  busy/progress state; surface per-file upload errors; revoke object URLs on completion/destroy;
  then navigate to `/app/tickets`.
- i18n (en + ar): attach title/hint, remove, file-too-large, wrong-type, uploading, upload-failed
  (mirror existing `portal.submit.*` keys and the `chat.*` RTL pattern).

**`detail.component.ts` / `.html` (US-406) — render attached images:**
- Add `attachments: TicketAttachmentInfo[]` to `PortalTicketDetail`.
- Render image attachments (content type starts with `image/`) as `<img>` with the blob/stream URL
  from `GET .../attachments/{id}/content` (loaded via `HttpClient` with the auth header, mirroring
  `customer-attachments.component.ts:234-252`), and non-image attachments as a download link.

### 6. Scope boundaries / rejected options

- **No single multipart create endpoint.** Creating the ticket first (existing JSON endpoint), then
  uploading to a dedicated attachment route, reuses the entire proven upload pipeline and avoids
  rewriting `CreateTicket` + its S1 acceptance tests. Upload-after-create is also the natural fit
  for portal UX while the ticket is being saved.
- **No TicketAttachment entity changes to `Asset` or `CustomerAttachment`** — the catalogue and the
  customer link stay untouched (their doc comments anticipate exactly this reuse).
- **Staff surface is IN scope (per approval).** Staff can upload and download ticket attachments
  through the InternalApi `TicketsController` actions above, and the dead
  `ticket-create.component.html:152-172` dropzone is wired to the same select/preview/validate/upload
  flow. No staff read-only limitation.

## Acceptance criteria

- **TA-1.** A signed-in customer can attach one or more image/PDF files on the portal submit form;
  the dropzone now has a real file input, selects the allowlisted types, and shows a per-file
  thumbnail/preview and a remove control. (frontend)
- **TA-2.** Files are validated client-side before upload: any file > 10 MB or with a
  non-allowlisted content type is refused with a clear message and is not uploaded. (frontend)
- **TA-3.** On successful ticket creation, each attached file is uploaded to
  `POST /api/portal/tickets/{id}/attachments`; the type/size rules are enforced server-side too
  (validator + handler), and any server refusal is surfaced as a field-level error without losing
  the other uploads. (both)
- **TA-4.** A ticket's attachments are stored as `TicketAttachment` + `Asset` rows; the bytes live
  outside the web root under a GUID name; a hostile filename cannot escape the storage directory
  (reused `LocalFileStore` containment). (backend)
- **TA-5.** A customer can only upload to and download from **their own** ticket: uploading to or
  reading another customer's ticket returns 404/403. (backend)
- **TA-6.** Downloads require a session (never a public static URL) and return the original
  filename in `Content-Disposition`. (backend)
- **TA-7.** The portal ticket detail view (US-406) renders attached images inline and offers a
  download for non-image attachments. (frontend)
- **TA-8.** Every new UI string exists in both `en` and `ar`. (frontend)
- **TA-9.** Staff can upload ticket attachments through the InternalApi
  `POST api/Tickets/{id}/attachments` (multipart, same size/type refusals) and the staff
  ticket-create dead dropzone is wired to select/preview/validate/upload. (both)
- **TA-10.** Staff can list and download any ticket's attachments through the InternalApi
  `GET api/Tickets/{id}/attachments` and `.../content` actions, and the staff ticket-detail surface
  renders attached images / offers download. (both)
- **TA-11.** Staff uploads require an authenticated session and a ticket that exists (404
  otherwise); the same storage containment, size and allowlist rules apply as to portal uploads.
  (backend)

## Assumptions / decisions to confirm

- Attachments upload **after** the ticket is created (its own endpoint), not inline with the create
  request.
- Scope is **both surfaces** (portal submit + detail, and staff create + detail), per approval.
- Portal actions are ownership-scoped to the signed-in customer; staff actions are scoped to role
  + ticket existence, not customer ownership.
- Image attachment `<img>` loading from the content endpoint uses `HttpClient` with the session/auth
  header, not a bare `src`.
- Uploads match the same 10 MB + allowlist policy as customer attachments on both hosts.
