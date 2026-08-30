# Task 05 — ContentDto completion (IsFaq, CategoryId, CategoryName)

## Traceability
Epic:   docs/requirements/epics/EPIC-06-knowledge-base.md
Story:  docs/requirements/user-stories/US-503-category-tag-management.md (AC-171..174)
FEAT:   FEAT-18 — delivery-plan.md row 11; leftover of KB plan Task 9
Spec:   docs/superpowers/specs/EPIC-06-US-504-knowledge-base.md
Plan:   docs/superpowers/plans/EPIC-06-US-504-feat-11-knowledge-base/

## Work
Append to the positional record (add at END so existing constructions don't shift):
```csharp
    …, int Version, int DislikeCount,
    bool IsFaq, Guid? CategoryId, string? CategoryName);
```
Populate via a ContentCategories join in GetContents/GetContentById/GetFaqContents handlers:
```csharp
var categoryName = await db.ContentCategories
    .Where(c => c.Id == content.CategoryId).Select(c => c.Name)
    .SingleOrDefaultAsync(ct);
```
(If handlers share one ToDto mapping site, extend that single site.)

## Tests (failing first)
AC503_ArticleExposesCategoryName — article created via CategoryId exposes CategoryName in DTO.

## Gate
dotnet build (0 errors) + new test + ContentCategory/KB endpoint suites green.
