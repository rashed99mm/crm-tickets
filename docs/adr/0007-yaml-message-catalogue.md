# ADR 0007 — YAML message catalogue, both languages in every response

- **Status:** Accepted
- **Date:** 2026-08-24

## Context

The application must produce user-facing text in Arabic and English (brief area 12). Two questions
had to be settled: where the text lives, and how the client gets the right language.

Handlers must not contain prose. A message hardcoded in a handler cannot be reviewed by a
translator, cannot be changed without a rebuild of that layer, and duplicates itself the moment two
handlers report the same condition.

## Decision

**One flat `Resources.yml`**, keyed by domain key, both languages inline:

```yaml
REQUIRED_FIELD:
  ar: "هذا الحقل مطلوب"
  en: "This field is required"
```

**Both languages ship in every response** as `message: { ar, en }`. The client selects; the server
does no content negotiation.

Handlers reference domain keys (`CUSTOMER_NOT_FOUND`), never numeric codes or prose. `SystemCodeMap`
translates the key to the wire code, and `IMessageCatalog` resolves it to text. The catalogue parses
once at startup, and malformed YAML or a duplicate key fails startup rather than the first request
that needs the string.

A guard test asserts every code constant has an entry with non-empty `ar` and `en`.

## Alternatives considered

| Option | Why it lost |
|---|---|
| **`.resx` with `IStringLocalizer`** | The built-in .NET answer, with tooling support. It lost on the file format: `.resx` is XML that reviewers cannot read comfortably and that produces awful merge conflicts. YAML with two sibling keys is legible to a translator who does not write code. |
| **One file per culture (`messages.en.yml`, `messages.ar.yml`) with `Accept-Language` negotiation** — my earlier proposal | Conventional, and keeps responses smaller. It lost because it needs `RequestLocalizationMiddleware`, a cross-file fallback chain, and a refetch whenever the user switches language. Sending both languages removes all three at the cost of a few hundred bytes. |
| **Text in the database** | Editable without deploying, which matters for a CMS. Overkill here: it needs an admin UI to be useful, and puts a query in the error path — where a failing lookup during error handling turns a 409 into a 500. |
| **Codes only, translated entirely in the frontend** | Smallest payload, and the frontend already needs its own strings. It lost because the same message would then exist in two places, and any non-browser client (a webhook consumer, a support tool) would get an unreadable code with nothing to show a human. |

## Consequences

- Text is reviewable in one file by someone who does not read C#.
- The client switches language instantly, with no refetch and no server round trip.
- **A third language changes the response shape**, because languages are keys rather than a
  negotiated value. That is the accepted limit of this design; adding French would mean either a
  third key everywhere or migrating to negotiation.
- Every response is slightly larger. Negligible for text this short.
- The guard test makes a missing translation a build failure. This is the main safety property, and
  without it the failure mode is a blank message in front of a user — the worst possible place to
  discover it.
- The localizer must never throw. A formatting failure while building an error response would
  convert a well-handled 409 into a 500, so a missing placeholder argument leaves the token visible
  instead (FND-20).
- Arabic strings for S1 are placeholders pending review, and marked as such in the file. Shipping
  machine-translated Arabic as though it were reviewed would misrepresent the deliverable; real
  translation is S8's work.
