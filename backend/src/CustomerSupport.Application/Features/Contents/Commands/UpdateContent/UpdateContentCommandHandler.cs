using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Contents.Commands.UpdateContent;

public class UpdateContentCommandHandler(
    IRepository<Content> contentRepository,
    IRepository<ContentVersion> contentVersionRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IMessageFactory messages,
    ILogger<UpdateContentCommandHandler> logger)
    : ICommandHandler<UpdateContentCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(UpdateContentCommand request, CancellationToken ct)
    {
        logger.LogInformation("Updating content {ContentId}", request.Id);

        var content = await contentRepository.GetByIdAsync(request.Id, ct);
        if (content == null)
        {
            logger.LogWarning("Update failed — content {ContentId} not found", request.Id);
            return messages.NotFound<Guid>(ApplicationErrors.Content.NOT_FOUND);
        }

        var changedFields = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Title)) changedFields.Add("Title");
        if (request.Body != null) changedFields.Add("Body");
        if (request.Summary != null) changedFields.Add("Summary");
        if (request.Category != null) changedFields.Add("Category");
        if (request.Tags != null) changedFields.Add("Tags");
        if (!string.IsNullOrEmpty(request.Status)) changedFields.Add("Status");
        if (request.FeaturedImageUrl != null) changedFields.Add("FeaturedImageUrl");
        if (request.ExpiresAt.HasValue) changedFields.Add("ExpiresAt");
        if (request.IsFeatured.HasValue) changedFields.Add("IsFeatured");

        content.UpdateContent(request.Title, request.Body, request.Summary, request.Category);

        if (request.Tags != null)
        {
            content.UpdateTags(request.Tags);
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            content.UpdateStatus(request.Status);
        }

        if (request.FeaturedImageUrl != null)
        {
            content.UpdateFeaturedImage(request.FeaturedImageUrl);
        }

        if (request.ExpiresAt.HasValue)
        {
            content.UpdateExpiresAt(request.ExpiresAt);
        }

        if (request.IsFeatured.HasValue)
        {
            content.UpdateIsFeatured(request.IsFeatured.Value);
        }

        // AC-168/170 — every save produces a new version, named after what actually changed.
        var changeSummary = changedFields.Count > 0
            ? $"Updated: {string.Join(", ", changedFields)}"
            : "Updated";
        var snapshot = content.RecordChange(changeSummary);
        var version = ContentVersion.Create(
            content.Id, snapshot.VersionNumber, userContext.UserId, snapshot.ChangeSummary, snapshot.Title, snapshot.Body);
        await contentVersionRepository.AddAsync(version, ct);

        contentRepository.Update(content);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Content {ContentId} updated successfully", content.Id);

        return messages.Success(content.Id, ApplicationErrors.Content.UPDATED);
    }
}
