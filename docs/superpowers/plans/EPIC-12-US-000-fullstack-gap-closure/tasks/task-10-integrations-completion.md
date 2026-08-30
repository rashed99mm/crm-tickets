# Task 10 - Integrations Completion

**Status:** Ready  
**Closes gaps:** Hardcoded integration cards, ERP connector, external API configs, hardcoded API key, decorative Add/Configure/Resume buttons.

## Files

- Backend domain: `ExternalApiConfiguration.cs`
- Backend API: `ExternalApiConfigurationsController.cs`
- Frontend API: new/extended `common/src/lib/admin/external-api-configuration.api.ts`
- Frontend UI: `features/admin/platform-settings.component.*` or new `features/admin/integrations.component.*`

## Implementation

- Add typed provider configs for Gmail, WhatsApp, SMS, ERP, custom webhook/API.
- Add masked secret fields and rotate/revoke endpoints.
- Add provider health check status.
- Build integrations list and provider dialogs.
- Remove hardcoded `sk_live_...`.

## Code Example

```ts
export interface ExternalApiConfigurationDto {
  readonly id: string;
  readonly provider: 'Gmail' | 'WhatsApp' | 'Sms' | 'Erp' | 'Custom';
  readonly displayName: string;
  readonly status: 'Connected' | 'NotConfigured' | 'NeedsAuth' | 'Error';
  readonly maskedSecret?: string | null;
  readonly lastCheckedAt?: string | null;
}
```

## Acceptance

- [ ] Integration cards render from API.
- [ ] Add opens provider picker.
- [ ] Configure saves provider fields.
- [ ] Resume opens incomplete provider step.
- [ ] Secrets are masked and never hardcoded.
- [ ] Health state is refreshed from backend.

## Evidence

Pending.
