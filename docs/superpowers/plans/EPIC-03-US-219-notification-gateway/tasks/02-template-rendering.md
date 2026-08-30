# Task 02 — Notification Templates

**Criteria:** `NG-1`, `NG-7`

## Files

- `Application/Notifications/INotificationTemplateRenderer.cs`
- `Infrastructure/Notifications/NotificationTemplateRenderer.cs`
- Existing notification template entity/configuration and renderer tests.

## Steps

1. Write failing tests for known variables, missing variables, empty templates, and HTML/text
   channel rendering.
2. Query active templates by `(TemplateCode, Channel)` using an `AsNoTracking` projection.
3. Replace only approved `{{Name}}` tokens; reject unknown tokens rather than silently removing them.
4. Return a `RenderedNotification` with subject/body and no raw secret variables in its audit snapshot.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~TemplateRenderer"`  
**Expected:** Rendering is deterministic and missing variables fail before delivery.

**Commit:** `feat: render localized notification templates`
