# Portal + Staff Ticket Attachments — backend plan

**Date:** 2026-08-29
**Spec:** `docs/superpowers/specs/EPIC-02-US-016-portal-ticket-attachments.md` (TA-1..TA-11)
**Approach:** reuse the existing `Asset` + link-entity pipeline (`EPIC-02-US-001-customer-attachments.md`),
which already gives size/type refusal, GUID storage and path containment.

## Grounding (touched files)
- Reference upload: `Application/Features/Customers/Commands/AddCustomerAttachment/AddCustomerAttachmentCommandHandler.cs`
  (mirror for size/type refusal, stream handling, orphan cleanup).
- Reference catalogue: `Domain/Entities/Assets/Asset.cs` (unchanged).
- Reference link: `Domain/Entities/Customers/CustomerAttachment.cs` (model `TicketAttachment` on it).
- Reference EF config: `Infrastructure/Persistence/Configurations/AssetConfiguration.cs:33-57`
  (`CustomerAttachmentConfiguration`), and `Infrastructure/Persistence/AppDbContext.cs:33-44` (DbSets).
- Reference download/list: `Application/Features/Customers/Queries/DownloadCustomerAttachment/` and
  `.../GetCustomerAttachments/`.
- Reference DTOs: `Application/Features/Customers/Dtos/CustomerAttachmentDtos.cs`
  (`CustomerAttachmentDto` line 14, `AttachmentContentDto` line 30).
- Error codes: `Application/Errors/ApplicationErrors.cs` — `Attachment.*` (ATTACHMENT_NOT_FOUND /
  ATTACHMENT_EMPTY / ATTACHMENT_TOO_LARGE / ATTACHMENT_TYPE_NOT_ALLOWED / ATTACHMENT_ADDED) reused
  verbatim; `Ticket.NOT_FOUND` for the ticket-missing case.

## Tasks

### T-B1 — Domain: `TicketAttachment`
New `Domain/Entities/Tickets/TicketAttachment.cs`, mirroring `CustomerAttachment.Create`: a thin
link with `TicketId`, `AssetId`, `Create(ticketId, assetId)` factory.

### T-B2 — EF wiring
- `AppDbContext.cs`: `public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();`
- `AssetConfiguration.cs`: add `TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>`
  mirroring the customer one — table `TicketAttachments`, unique filtered `UX_TicketAttachments_Asset`
  on `AssetId`, `IX_TicketAttachments_Ticket` on `TicketId`, FKs to `Ticket` and `Asset`
  (`DeleteBehavior.Restrict`).
- Migration `AddTicketAttachments` (via `dotnet ef migrations add`).

### T-B3 — Application: command + handler
New `Application/Features/Tickets/Commands/AddTicketAttachment/`:
- `AddTicketAttachmentCommand(Guid TicketId, string FileName, string ContentType, long DeclaredLength, Stream Content)
  : ICommand<Response<Guid>>`.
- Handler mirrors `AddCustomerAttachmentCommandHandler` with these differences:
  - resolves `users`/ticket by `TicketId`; missing → `Attachment.NOT_FOUND` via `messages.NotFound`.
  - optional `customerId?: Guid` — when provided (portal), the ticket must belong to that customer,
    else `Attachment.NOT_FOUND` (TA-5 scoping). Staff pass null.
  - empty / too-large / non-allowlisted refusals identical to customer handler.
  - `Asset.Create`, `TicketAttachment.Create`, `fileStore.SaveAsync`, persist + `SaveChangesAsync`,
    orphan-cleanup on failure.

### T-B4 — Application: list + download queries
- `Application/Features/Tickets/Queries/GetTicketAttachments/GetTicketAttachmentsQuery(TicketId, CustomerId?, ...)` →
  `Response<IReadOnlyList<TicketAttachmentDto>>` (reuse `CustomerAttachmentDto` shape or a new one).
  Join `TicketAttachment` + `Asset`, optional `CustomerId` ownership filter (TA-5/TA-10).
- `Application/Features/Tickets/Queries/DownloadTicketAttachment/DownloadTicketAttachmentQuery(TicketId, AttachmentId, CustomerId?)` →
  `Response<AttachmentContentDto>`, mirroring `DownloadCustomerAttachmentQueryHandler` with the ownership guard.

### T-B5 — API endpoints
- `ExternalApi/Controllers/PortalController.cs`: `POST api/portal/tickets/{id}/attachments`
  (`[Consumes("multipart/form-data")]`, `IFormFile? file`, `[RequestSizeLimit]`), `GET
  api/portal/tickets/{id}/attachments`, `GET api/portal/tickets/{id}/attachments/{attachmentId}/content`.
  Portal actions pass `customerId` from the token (owner scoping, TA-5).
- `InternalApi/Controllers/TicketsController.cs`: same three actions (`api/Tickets/...`) without a
  customer filter (staff scope, TA-9/TA-10/TA-11).

### T-B6 — Verify
- `dotnet build` clean, then live verify against the running hosts: create a customer ticket via the
  portal, upload an image, list + download it; same via staff; confirm 404 on another customer's
  ticket; run the existing backend test suite (per workflow) after the manual checks.
