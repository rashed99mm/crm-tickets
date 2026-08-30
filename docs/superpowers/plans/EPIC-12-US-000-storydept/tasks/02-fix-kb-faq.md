# Task 02 — Fix KB FAQ endpoint (AC177 ×2)

## Traceability
Epic:   docs/requirements/epics/EPIC-06-knowledge-base.md
Story:  docs/requirements/user-stories/US-504-faq-management.md
FEAT:   FEAT-18 (Knowledge base) — delivery-plan.md row 11
Spec:   docs/superpowers/specs/EPIC-06-US-504-knowledge-base.md
Plan:   docs/superpowers/plans/EPIC-06-US-504-feat-11-knowledge-base/

## Work
`GET /api/knowledge-base/articles/faq` (KnowledgeBaseController.cs ~line 105) must return only
`IsFaq && Published`; unmarking must remove from the endpoint. Suspect: the handler filters on the
deprecated free-string `Category`/wrong Status comparison instead of `IsFaq`.
Files: GetFaqContentsQueryHandler, Content entity flag methods (MarkAsFaq/UnmarkFaq exist).

## Tests (red, make green — do not weaken)
ContentFaqEndpointTests.AC177_FaqEndpoint_ReturnsOnlyFaqArticles
ContentFaqEndpointTests.AC177_UnmarkFaq_RemovesFromFaqEndpoint

## Gate
dotnet test --filter "FullyQualifiedName~ContentFaqEndpointTests" → green, output pasted.
