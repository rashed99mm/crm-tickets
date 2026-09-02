using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Channels.Commands.IngestTwilioSms;

/// <summary>
/// CC-40/CC-41 — one inbound SMS delivery, still in Twilio's own vocabulary.
///
/// The whole form is carried, not just <c>From</c>/<c>Body</c>/<c>MessageSid</c>, because Twilio's
/// signature covers **every** posted parameter: dropping the ones this channel does not read would
/// change the signed material and fail every check.
///
/// <c>RequestUrl</c> is supplied by the caller for the same reason — Twilio signs the URL it was
/// configured to post to, and the Application layer has no <c>HttpRequest</c> to reconstruct it
/// from.
/// </summary>
public record IngestTwilioSmsCommand(
    IReadOnlyDictionary<string, string> Form,
    string? Signature,
    string RequestUrl) : ICommand<Response<Guid>>;
