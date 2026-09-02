using System.Collections.Concurrent;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Interfaces;

namespace CustomerSupport.Infrastructure.Channels;

/// <summary>
/// A per-client fixed window, held in memory (CC-47, spec A24). Limits match the platform's
/// existing "login" policy (WebApiServiceExtensions.cs:63-72) — five attempts per five minutes per
/// IP — because both guard an anonymous endpoint against the same kind of abuse.
///
/// In memory, and therefore per process: two hosts behind a load balancer each keep their own
/// window. That is accepted here rather than adding a distributed cache, because the defence's
/// purpose is to blunt casual abuse of a demo-stage form, and IMemoryCache is not registered in this
/// solution either. Registered as a singleton, so the dictionary survives between requests.
/// </summary>
public sealed class WebFormSubmissionThrottle(IDateTimeService clock) : IWebFormSubmissionThrottle
{
    public const int PermitLimit = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, Counter> _counters = new();

    public bool TryAcquire(string clientKey)
    {
        var counter = _counters.GetOrAdd(clientKey, _ => new Counter());
        var now = clock.UtcNow;

        lock (counter)
        {
            if (now - counter.WindowStart >= Window)
            {
                counter.WindowStart = now;
                counter.Count = 0;
            }

            if (counter.Count >= PermitLimit)
            {
                return false;
            }

            counter.Count++;
            return true;
        }
    }

    private sealed class Counter
    {
        public DateTime WindowStart { get; set; } = DateTime.MinValue;
        public int Count { get; set; }
    }
}
