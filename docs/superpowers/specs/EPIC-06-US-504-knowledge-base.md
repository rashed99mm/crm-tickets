# Knowledge base — versioning, taxonomy, FAQ, ticket links, search, engagement, admin UI

**Feature:** `FEAT-11` (Knowledge base) · **Epic:** `EPIC-06` · **Stories:** `US-501`–`US-513`
**Not implemented this pass** — spec and implementation plan only, per explicit instruction.

## Problem

The platform's inherited `Content` entity and `ContentsController`/`KnowledgeBaseController` give it a working
article store — create, update, publish/archive (via a generic status field), and a published-only anonymous
read surface — but none of what `EPIC-06`'s thirteen stories actually ask for exists on top of it: there is no
version history, no real taxonomy (category is one free-text string, tags are an unmanaged array), no FAQ
concept, no link between an article and the ticket it solved, no search beyond a SQL `LIKE`, and — despite the
entity already carrying `ViewCount`/`LikeCount` fields with increment methods — nothing in the codebase ever
calls them. No admin screen exists to manage any of it, and the one portal screen that already exists
(`kb-list.component`/`kb-detail.component`, built by a concurrent session) reads the wrong endpoint.

## Assumptions

A1. **Every story's own SQL sketch invents a parallel `KnowledgeArticles` table**, as if no content entity
    existed yet. It does — `Content` (`CustomerSupport.Domain/Entities/Content/Content.cs`), already carrying
    `Title`, `Body`, `Summary`, `Status` (`Draft`/`Published`/`Archived` via `ContentStatus`), `AuthorId`,
    `ViewCount`, `LikeCount`, `Tags` (JSON string array), `Category` (free string), `FeaturedImageUrl`,
    `PublishedAt`/`ExpiresAt`, `IsFeatured`. This spec extends `Content`, never creates a competing table —
    the same call already made for `US-608` (report scoping over the real `Ticket`/`ApplicationUser` shape,
    not the story's invented one) and `US-607` (rejected outright rather than built against a schema that
    doesn't match).

A2. **KB categories are a new, dedicated entity — not a reuse of the ticket `Category` entity, and not the
    existing `Content.Category` free-text field.** The ticket `Category` (Technical/Billing/etc.) is a flat,
    unrelated taxonomy for routing tickets; conflating it with KB article topics would make both worse. `US-503`
    asks for a *hierarchical* category tree, which the ticket entity was never designed for. New entity:
    `ContentCategory` (self-referencing `ParentId`, matching `Department`/`Branch`'s established lookup-entity
    shape from `FEAT-16` — `BaseEntity`, explicit `IsActive`). `Content.Category` (the free string) is replaced
    by `Content.CategoryId` (nullable FK) in the same migration; existing free-text values are not
    back-migrated into the new taxonomy (no reliable mapping exists) — they become `null` and an admin
    re-categorizes as needed. Recorded as a real data-migration decision, not hidden.

A3. **Tags stay a flat string array on `Content`, not a normalized `Tags`/`ArticleTags` join.** The stories'
    own schema sketch (`Tags`, `ArticleTags`) buys referential tag identity (rename-once, updates everywhere)
    that nothing in the thirteen stories' acceptance criteria actually requires — every AC that touches tags
    (`US-503` AC3/AC5, `US-510` AC5) only needs "add a tag to an article" and "list an article's tags," both of
    which the existing JSON-array column already satisfies. Normalizing now is scope the stories don't ask for;
    revisit if a future story needs tag rename/merge or a tag-browse screen.

A4. **`US-506` "Arabic-aware search" ships as diacritic folding over the existing `LIKE`-based search, not
    SQL Server full-text search.** No full-text catalog exists anywhere in this codebase, and standing one up
    (catalog creation, population schedule, Arabic word-breaker configuration) is real infrastructure this
    project has never needed before. What ships: a small normalization step (strip Arabic tashkeel/diacritics
    from both the stored text at index time — computed, not stored twice — and the query at search time)
    layered onto the existing `Title.Contains(term) || Body.Contains(term)` pattern. This satisfies the story's
    own AC1 (diacritic folding) and AC2–AC4 (English unaffected, mixed results, empty-list-not-error) exactly;
    it does not deliver relevance ranking or stemming, which nothing in the stories' ACs actually tests for.

A5. **`US-507` (view tracking) and `US-508` (helpfulness voting) wire behavior onto fields that already exist
    (`ViewCount`, `LikeCount`), and add what's genuinely missing.** `LikeCount` covers "helpful"; there is no
    "unhelpful" counterpart today, so `Content` gains `DislikeCount` (mirroring `LikeCount`'s existing
    increment/decrement shape). Per-user "one vote per article, changeable" (`US-508` AC3/AC4) needs a real
    per-user record — nothing in the entity today stops the same user incrementing `LikeCount` infinitely — so
    a new `ContentVote` table backs the denormalized counts, exactly as the story's own `ArticleVotes` sketch
    intended, just against `Content` rather than a `KnowledgeArticles` table. View tracking similarly gets a
    `ContentView` detail table alongside the counter, per `US-507` AC2/AC3 (anonymous views recorded with a
    null user id).

A6. **Inbound webhook and background-poller mechanics for `US-204` are out of scope of the *Knowledge Base*
    spec** — that story lives in `EPIC-03`/`EPIC-10`, not `EPIC-06`, and is covered by the sibling
    `EPIC-10-US-203-email-integration-design.md` spec instead. Listed here only so its absence from this document
    isn't read as an oversight.

A7. **A real defect in already-shipped frontend work is recorded, not fixed, by this spec.** The portal's
    existing `kb-list.component`/`kb-detail.component` (built by a concurrent session, `frontend/projects/
    portal-app/src/app/features/kb/`) call `ContentsApi` against `/api/Contents` (`InternalApi`, unauthenticated
    but *unfiltered* — draft and archived articles are readable) instead of `/api/knowledge-base/articles`
    (`ExternalApi`, published-only by construction). `US-513`'s implementation plan must repoint this component
    to the correct, already-published-filtered endpoint as its first step — not build a second, parallel portal
    screen. Not touched by this spec itself, since this pass writes specs and plans only.

## Out of scope

- `US-502`'s diffing UI — snapshots (`TitleSnapshot`/`BodySnapshot`) are stored so a full diff *could* be
  computed, but no diff renderer ships this pass; the edit screen shows a flat version list only.
- Rate-limiting on view/vote endpoints (both stories' own Notes sections flag this as a future concern, not
  an AC).
- Full-text relevance ranking (A4).
- Tag rename/merge tooling (A3).
- Backfilling `Content.CategoryId` from the old free-text `Category` values (A2).

## Acceptance criteria

**Publish/archive (`US-501`, `US-509`, `US-512`)**

AC-165. Given an article in `Draft`, when the author issues `Publish`, then status becomes `Published`,
`PublishedAt` is stamped, and the transition is recorded as a version (`US-502`).

AC-166. Given an article in any status except `Archived`, when the author issues `Archive`, then status
becomes `Archived`, `ArchivedAt` is stamped, and the transition is recorded as a version.

AC-167. Given an article in `Archived`, when `Publish` is issued, then the request is rejected `409` and no
state changes.

**Versioning (`US-502`, `US-511`)**

AC-168. Given an article at version N, when the author saves any change (including a publish/archive
transition), then version becomes N+1 and a version record is created with author id, a change summary, and
title/body snapshots.

AC-169. Given a newly created article, then its version is `1`.

AC-170. Given an article with version history, when the edit view requests it, then versions are returned
newest-first with version number, author, timestamp, and change summary.

**Category/tag (`US-503`, `US-510`)**

AC-171. Given a KB author, when they create a `ContentCategory` with a name and optional parent, then it is
stored and retrievable; a duplicate `(Name, ParentId)` pair is rejected `409`.

AC-172. Given an article and an existing category, when the author assigns it, then `Content.CategoryId` is
set; an unknown category id is rejected `404`.

AC-173. Given an article, when the author adds a tag, then the tag appears in `Content.Tags`; duplicates
within the same article are not added twice (existing entity behavior, not new).

AC-174. Given categories with parent/child relationships, when listed, then they are returned as a nested
tree, not a flat list.

**FAQ (`US-504`)**

AC-175. Given a `Published` article, when the author marks it `IsFaq = true`, then the flag is set.

AC-176. Given a `Draft` or `Archived` article, when the author attempts to mark it FAQ, then the request is
rejected `409`.

AC-177. Given articles with `IsFaq = true`, when the FAQ endpoint is queried, then only those articles are
returned, and unmarking one removes it from that result.

**Article-ticket link (`US-505`)**

AC-178. Given a ticket and a `Published` article, when an agent links them, then a `ContentTicketLink` record
is created capturing the agent id and timestamp.

AC-179. Given a `Draft` article, when an agent attempts to link it to a ticket, then the request is rejected
`409`.

AC-180. Given an existing link, when an agent unlinks it, then the record is removed.

AC-181. Given a ticket with linked articles, when queried, then all links are returned with article
title/status; linking the same article to the same ticket twice is rejected `409` (unique constraint, matching
the story's own `TC-08`).

**Search (`US-506`)**

AC-182. Given an article whose Arabic text carries diacritics, when a diacritic-free query is searched, then
the article is matched (A4).

AC-183. Given English-only content, when an English query is searched, then matching is unaffected by the
Arabic-folding step.

AC-184. Given no article matches a query, then the endpoint returns an empty list, never an error.

**View tracking (`US-507`)**

AC-185. Given a `Published` article, when it is viewed (via `GET /api/knowledge-base/articles/{id}`), then
`Content.ViewCount` increments by 1 and a `ContentView` record is stored (user id if authenticated, null if
anonymous).

AC-186. Given the same article viewed twice by the same anonymous caller, then both views count independently
— no de-duplication this pass (not asked for by the story's own ACs).

**Helpfulness voting (`US-508`)**

AC-187. Given an authenticated user viewing a `Published` article, when they vote helpful or unhelpful, then
the corresponding count (`LikeCount`/`DislikeCount`) increments by 1 and a `ContentVote` row is stored.

AC-188. Given a user who already voted on an article, when they vote again (same or different direction),
then the previous vote's count is decremented, the new one incremented, and the `ContentVote` row is updated
in place — never a second row for the same `(ContentId, UserId)`.

**KB admin screens (`US-509`–`US-512`, frontend — planned, not built this pass)**

AC-189. Given the KB admin list, when it loads, then a paginated table shows title, status, category, author,
view count, last updated, with a status filter and an honest empty state (never rendering a failed load as
"no articles" — this project's `AsyncState` convention).

AC-190. Given the create form, when required fields (title, body, category) are missing, then inline
validation errors are shown and no request is sent; on valid submit, the article is created `Draft`.

AC-191. Given the edit form for a `Published` or `Archived` article, then editing is disabled with a
re-draft prompt — only `Draft` articles are directly editable, matching the story's own business rule.

AC-192. Given an article row, when its status is `Draft`, then Publish is enabled and Archive is enabled;
when `Archived`, both are disabled; Archive always confirms before calling the API.

**Portal browse (`US-513`, frontend — planned, not built this pass)**

AC-193. Given the portal KB screen, it reads `/api/knowledge-base/articles` (published-only,
`KnowledgeBaseController`) — never `/api/Contents` — fixing the defect in A7 as this story's first step.

AC-194. Given the portal detail view, when it loads, then title/body/category/tags/view count/helpfulness are
shown, the view is recorded (AC-185), and helpful/unhelpful controls are present and call `US-508`'s vote
endpoint.

AC-195. Given the portal's FAQ section, when it loads, then it lists only `IsFaq = true` articles, separately
from the general browse list.

## Design

### Backend: Domain

**`Content.Publish()`/`Content.Archive()` already exist** (`CustomerSupport.Domain/Entities/Content/Content.cs`)
with exactly the transition guard AC-165–167 need — `ContentStatus.CanTransitionTo` throws
`InvalidOperationException` on an illegal transition, stamps `PublishedAt`, raises the domain event. **No
domain change needed for AC-165–167** — what's missing is the CQRS command layer and controller actions that
call these already-correct methods; `UpdateContent`/`UpdateStatus` (the generic path `UpdateContentCommand`
already uses) stay as they are for every other field.

**Edit** `Content`: add `CategoryId` (`Guid?`, FK to new `ContentCategory`) alongside the existing `Category`
(string) — `Category` is deprecated in the same migration (its column stays briefly for the read path during
rollout, per A2, then a follow-up migration drops it once `US-509`–`512`'s admin screens are the only writers);
`IsFaq` (bool, default false); `DislikeCount` (int, default 0, mirroring `LikeCount`'s existing shape) with
`IncrementDislikeCount()`/`DecrementDislikeCount()`; `Version` (int, default 1) bumped by a new
`RecordChange(string changeSummary)` method called from every mutating command (`Create`, `UpdateContent`,
`Publish`, `Archive`).

**New:** `ContentCategory` (`BaseEntity`, `Name`, `Slug`, `ParentId` self-FK, `SortOrder`, `IsActive`/
`Deactivate()` — same shape as `Department`/`Branch`). `ContentVersion` (`Id`, `ContentId`, `VersionNumber`,
`AuthorId`, `ChangeSummary`, `TitleSnapshot`, `BodySnapshot`, `CreatedAt` — `IAppendOnlyEntity`, matching
`TicketHistory`/`SLAEvent`'s guard). `ContentView` (`Id`, `ContentId`, `UserId?`, `ViewedAt` —
`IAppendOnlyEntity`). `ContentVote` (`Id`, `ContentId`, `UserId`, `IsHelpful` bool, `VotedAt` — **not**
append-only, since AC-188 requires updating a row in place; unique index on `(ContentId, UserId)`).
`ContentTicketLink` (`Id`, `TicketId`, `ContentId`, `LinkedByAgentId`, `LinkedAt` — `IAppendOnlyEntity`; unique
index on `(TicketId, ContentId)` backing AC-181's `409`).

### Backend: Application (new CQRS features under `Features/Contents/` and `Features/ContentCategories/`)

`Commands/PublishContent`, `Commands/ArchiveContent` (AC-165/166/167). `Commands/CreateContentCategory`,
`Queries/GetContentCategoryTree` (AC-171, AC-174). `Commands/SetFaqFlag` (AC-175/176), `Queries/GetFaqArticles`
(AC-177). `Commands/LinkContentToTicket`, `Commands/UnlinkContentFromTicket`, `Queries/GetLinkedContent`
(AC-178–181). `Commands/RecordContentView` (AC-185/186). `Commands/VoteOnContent` (AC-187/188, upsert
semantics). `Queries/SearchContents` (AC-182–184, wraps the existing `GetContentsQueryHandler`'s `LIKE`
predicate with an Arabic-diacritic-folding normalization helper applied to both the stored text comparison and
the incoming term). Every existing `Content` mutation (`CreateContentCommandHandler`,
`UpdateContentCommandHandler`, plus the two new Publish/Archive handlers) calls `Content.RecordChange(...)`
and persists a `ContentVersion` row in the same `SaveChangesAsync` (AC-168/169).

### Backend: API

`ContentsController` (`InternalApi`) gains `POST /{id}/publish`, `POST /{id}/archive`, `GET /{id}/versions`,
`POST /categories`, `GET /categories` (tree), `POST /{id}/faq`, `GET /faq`, `POST /tickets/{ticketId}/content/
{contentId}/link`, `DELETE .../link`, `GET /tickets/{ticketId}/content`. `KnowledgeBaseController`
(`ExternalApi`, anonymous) gains `POST /articles/{id}/view` (or folds into the existing `GET /articles/{id}`
per AC-185 — cheaper, one round trip, matching how `US-513`'s AC6 expects the detail load itself to count as
the view) and `POST /articles/{id}/vote` (requires the portal caller to be an authenticated customer —
`US-403`'s auth boundary, not anonymous, since AC-188's per-user upsert needs an identity).

### Data model

One migration: `ContentCategory`, `ContentVersion`, `ContentView`, `ContentVote`, `ContentTicketLink` tables;
`Content` gains `CategoryId` (replacing `Category`), `IsFaq`, `DislikeCount`, `Version`. Existing `Category`
string values are dropped, not migrated (A2) — reviewed before the migration is ever applied, per this
project's established migration-review discipline (`FEAT-16`/`FEAT-17`'s task records).

### Error behavior

New failure codes needed (register in `SystemCode`/`SystemCodeMap`/`ResponseExtensions.MapFailureStatusCode`
per the `FEAT-16` lesson, or every one of these silently falls back to `400`): `CONTENT_NOT_PUBLISHED` (409,
AC-167/176/179), `CATEGORY_NOT_FOUND` (404, AC-172), `CATEGORY_NAME_EXISTS` (409, AC-171), `LINK_EXISTS` (409,
AC-181).
