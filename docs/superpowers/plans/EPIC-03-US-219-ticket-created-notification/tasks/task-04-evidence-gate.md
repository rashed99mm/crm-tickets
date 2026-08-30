# Task 04 — Evidence gate: end-to-end ticket creation → notification row + SignalR

**Criteria:** `AC-N2`, `AC-N3`, full-suite green, clean build, story status.

## Files

- `backend/tests/CustomerSupport.Tests/Integration/TicketCreatedNotificationTests.cs` — new.
- `docs/requirements/delivery-plan.md`, story status files — status update.

## Steps

1. **AC-N2 integration test** (mirror `SlaNotificationTests` / `CrmApiFactory` style, real LocalDB):
   - create a fresh admin + a customer;
   - create a portal user and `LinkCustomer(customerId)` via `AppDbContext`;
   - `POST /api/Tickets` (staff) with that customer;
   - assert a durable `Notification` row exists: `UserId` = linked user id, `Channel == "InApp"`,
     `NotificationType == "TICKET_CREATED"`, `Status == "Sent"`, and `Message` contains the reference.
   - **AC-N5:** the same flow with a customer that has *no* linked user produces **zero**
     Notification rows for that customer, and the create still returns 201.
2. **AC-N3 SignalR assertion:** subscribe a real `@microsoft/signalr` client in group
   `user:{customerUserId}`; create the ticket; assert `NotificationReceived` fires with the payload.
   This proves the gateway → `InAppNotificationChannelSender` → `RealTimeNotifier`
   (`Api.Shared/Notifications/RealTimeNotifier.cs:24`) path reaches a live client — the exact failure
   the original report described.
3. Run the backend suite in full; paste the output here.
4. `dotnet build backend/CustomerSupport.slnx --warnaserror` — must be clean.
5. Update `docs/requirements/delivery-plan.md` FEAT-15 row and any story file status from the
   **observed** output (never assumed).

**Run:**
`dotnet test backend/CustomerSupport.slnx`
then
`dotnet build backend/CustomerSupport.slnx --warnaserror`

**Commit:** `feat: ticket-created notification evidence (AC-N1..AC-N6)`

**Deviation log:** (fill after execution)
