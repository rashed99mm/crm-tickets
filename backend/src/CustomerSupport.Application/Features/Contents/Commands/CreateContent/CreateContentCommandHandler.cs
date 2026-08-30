using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Contents.Commands.CreateContent;

public class CreateContentCommandHandler(
    IRepository<Content> contentRepository,
    IRepository<ContentVersion> contentVersionRepository,
    IUnitOfWork unitOfWork,
    IMessageFactory messages,
    ILogger<CreateContentCommandHandler> logger)
    : ICommandHandler<CreateContentCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateContentCommand request, CancellationToken ct)
    {
        logger.LogInformation("Creating content by author {AuthorId}", request.AuthorId);

        var contentExists = await contentRepository.ExistsAsync(c => c.Title == request.Title, ct);
        if (contentExists)
        {
            logger.LogWarning("Content creation failed — title already exists");
            return messages.Fail<Guid>(ApplicationErrors.Content.ALREADY_EXISTS, MessageType.Conflict);
        }

        var content = Content.Create(
            request.Title,
            request.Body,
            request.ContentType,
            request.AuthorId,
            request.Summary,
            request.Category);

        if (request.FeaturedImageUrl != null)
        {
            content.UpdateContent(null, null, null, request.Category);
        }

        await contentRepository.AddAsync(content, ct);

        // AC-169 — the initial save is version 1, not RecordChange's post-increment 2.
        var initialVersion = ContentVersion.Create(
            content.Id, content.Version, request.AuthorId, "Created", content.Title, content.Body);
        await contentVersionRepository.AddAsync(initialVersion, ct);

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Content {ContentId} created successfully by author {AuthorId}", content.Id, request.AuthorId);

        return messages.Success(content.Id, ApplicationErrors.Content.CREATED);
    }
}
