using System.Security.Cryptography;
using CustomerSupport.Domain.Entities;

namespace CustomerSupport.Domain.Entities.Channels;

public class LiveChatSession : BaseEntity
{
    private static readonly string[] AllowedStatuses = ["Waiting", "Active", "Closed", "Abandoned"];

    public string SessionTokenHash { get; private set; } = string.Empty;
    public string? CustomerName { get; private set; }
    public string? CustomerEmail { get; private set; }
    public string Status { get; private set; } = "Waiting";
    public Guid? ClaimedByAgentId { get; private set; }
    public DateTime? ClaimedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    private LiveChatSession() { }

    public static (LiveChatSession Session, string Token) Start(string? customerName, string? customerEmail)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        var session = new LiveChatSession
        {
            Id = Guid.NewGuid(),
            SessionTokenHash = HashToken(token),
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim(),
            CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail.Trim().ToLowerInvariant(),
            Status = "Waiting",
            CreatedAt = now,
        };

        return (session, token);
    }

    public static string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("A live chat token is required", nameof(token));
        }

        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    }

    public bool MatchesToken(string token) => SessionTokenHash == HashToken(token);

    public void Claim(Guid agentId)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent is required", nameof(agentId));
        }

        if (Status is "Closed" or "Abandoned")
        {
            throw new InvalidOperationException("Closed live chat sessions cannot be claimed.");
        }

        if (Status == "Active" && ClaimedByAgentId is not null && ClaimedByAgentId != agentId)
        {
            throw new InvalidOperationException("Live chat session is already claimed.");
        }

        Status = "Active";
        ClaimedByAgentId = agentId;
        ClaimedAt ??= DateTime.UtcNow;
        MarkUpdated();
        UpdatedBy = agentId;
    }

    public void Close(Guid agentId)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent is required", nameof(agentId));
        }

        if (Status == "Closed")
        {
            return;
        }

        if (Status == "Abandoned")
        {
            throw new InvalidOperationException("Abandoned live chat sessions cannot be closed.");
        }

        Status = "Closed";
        ClosedAt = DateTime.UtcNow;
        MarkUpdated();
        UpdatedBy = agentId;
    }

    public void EnsureOpenForCustomer()
    {
        if (!AllowedStatuses.Contains(Status))
        {
            throw new InvalidOperationException($"Unknown live chat status '{Status}'.");
        }

        if (Status is "Closed" or "Abandoned")
        {
            throw new InvalidOperationException("Live chat session is closed.");
        }
    }
}
