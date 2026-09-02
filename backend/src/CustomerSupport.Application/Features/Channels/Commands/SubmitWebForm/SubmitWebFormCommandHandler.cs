using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;
using CustomerSupport.Application.Features.Tickets.Queries.GetTicketReferenceForMessage;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Channels.Commands.SubmitWebForm;

/// <summary>
/// The web form's whole policy: honeypot, then rate window, then the real submission (CC-47).
///
/// This lives in a handler rather than in <c>WebFormController</c> because it is decision-making,
/// not request binding — which is all a controller in this codebase is allowed to do. It also means
/// the policy is testable without an HTTP host.
///
/// It dispatches the shared ingestion command through <see cref="IMediator"/> rather than
/// re-implementing it. No other handler here does that, so it is worth saying why: CC-1..CC-4
/// deliberately have **one** ingestion path, complete with its own validator, and four callers
/// (WhatsApp, SMS, email, this) must not each grow their own copy of customer matching and ticket
/// threading. Sending the command keeps that path single-sourced and keeps its validation in play.
/// </summary>
public class SubmitWebFormCommandHandler(
    IMediator mediator,
    IWebFormSubmissionThrottle throttle,
    IMessageFactory messageFactory,
    ILogger<SubmitWebFormCommandHandler> logger)
    : ICommandHandler<SubmitWebFormCommand, Response<WebFormSubmissionResult>>
{
    public async Task<Response<WebFormSubmissionResult>> Handle(
        SubmitWebFormCommand request, CancellationToken ct)
    {
        // CC-22 — a populated honeypot is a bot: the field is hidden from real users. Checked before
        // the throttle so a bot cannot spend a human's budget.
        if (!string.IsNullOrWhiteSpace(request.Honeypot))
        {
            logger.LogInformation("Web-form submission discarded: honeypot populated");
            return PretendAccepted();
        }

        if (!throttle.TryAcquire(request.ClientKey))
        {
            logger.LogInformation("Web-form submission discarded: client over its window budget");
            return PretendAccepted();
        }

        var ingested = await mediator.Send(
            new IngestInboundChannelMessageCommand(
                Channel: ChannelNames.WebForm,
                CustomerName: request.Name,
                CustomerPhone: null,
                CustomerEmail: request.Email,
                Body: request.Description,
                ProviderMessageId: null,
                Subject: request.Subject),
            ct);

        if (!ingested.Success)
        {
            // A genuine validation failure — a mistyped email — is the customer's to correct, so it
            // is reported as itself, field errors and all, not disguised as a success.
            return messageFactory.Validation<WebFormSubmissionResult>(
                ApplicationErrors.General.VALIDATION_ERROR, ingested.Errors);
        }

        // A25 — the shared command returns the message id; the customer needs the ticket reference.
        var reference = await mediator.Send(new GetTicketReferenceForMessageQuery(ingested.Data), ct);
        if (!reference.Success)
        {
            return messageFactory.NotFound<WebFormSubmissionResult>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        return messageFactory.Success(
            new WebFormSubmissionResult(reference.Data!, true),
            ApplicationErrors.Ticket.MESSAGE_RECORDED);
    }

    /// <summary>
    /// CC-47's indistinguishability requirement: a bot or a throttled caller must not be able to
    /// tell the defence fired. The reference matches the real generator's <c>TKT-nnnnnn</c> shape
    /// (<c>TicketReferenceGenerator.cs</c>) but is drawn at random and never persisted, so it
    /// consumes no sequence value and resolves to no ticket.
    /// </summary>
    private Response<WebFormSubmissionResult> PretendAccepted() =>
        messageFactory.Success(
            new WebFormSubmissionResult($"TKT-{Random.Shared.Next(0, 1_000_000):D6}", true),
            ApplicationErrors.Ticket.MESSAGE_RECORDED);
}
