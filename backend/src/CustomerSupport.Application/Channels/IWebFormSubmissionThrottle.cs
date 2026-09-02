namespace CustomerSupport.Application.Channels;

/// <summary>
/// Rate-limits anonymous web-form submissions per client (CC-22/CC-47). A port rather than the
/// framework's rate limiter because CC-47 requires a throttled caller to receive a response
/// indistinguishable from a successful one, and the ASP.NET middleware answers a distinguishable
/// 429 (spec A24). The caller decides what a refusal looks like; this only answers whether the
/// client has budget left.
/// </summary>
public interface IWebFormSubmissionThrottle
{
    /// <param name="clientKey">Stable identifier for the caller — the remote IP address in
    /// practice. Never a value from the payload, which an attacker chooses.</param>
    /// <returns>True when the submission is inside the window's budget, false when it is not.</returns>
    bool TryAcquire(string clientKey);
}
