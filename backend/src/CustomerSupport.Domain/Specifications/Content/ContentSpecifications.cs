using ContentEntity = CustomerSupport.Domain.Entities.Content.Content;

namespace CustomerSupport.Domain.Specifications.Content;

public class GetContentByIdSpec : BaseSpecification<ContentEntity>
{
    public GetContentByIdSpec(Guid contentId)
    {
        SetCriteria(c => c.Id == contentId);
    }
}

public class GetPublishedContentSpec : BaseSpecification<ContentEntity>
{
    public GetPublishedContentSpec()
    {
        SetCriteria(c => c.Status == "Published" && !c.IsDeleted);
        ApplyOrderByDescending(c => c.PublishedAt);
    }
}

public class GetFeaturedContentSpec : BaseSpecification<ContentEntity>
{
    public GetFeaturedContentSpec()
    {
        SetCriteria(c => c.IsFeatured && c.Status == "Published" && !c.IsDeleted);
        ApplyOrderByDescending(c => c.PublishedAt);
    }
}

public class GetContentByAuthorSpec : BaseSpecification<ContentEntity>
{
    public GetContentByAuthorSpec(Guid authorId)
    {
        SetCriteria(c => c.AuthorId == authorId && !c.IsDeleted);
        ApplyOrderByDescending(c => c.CreatedAt);
    }
}

public class GetContentByCategorySpec : BaseSpecification<ContentEntity>
{
    public GetContentByCategorySpec(string category)
    {
        SetCriteria(c => c.Category == category && c.Status == "Published" && !c.IsDeleted);
        ApplyOrderByDescending(c => c.PublishedAt);
    }
}

public class GetContentByTagSpec : BaseSpecification<ContentEntity>
{
    public GetContentByTagSpec(string tag)
    {
        SetCriteria(c => c.Tags.Contains(tag.ToLowerInvariant()) && c.Status == "Published" && !c.IsDeleted);
        ApplyOrderByDescending(c => c.PublishedAt);
    }
}
