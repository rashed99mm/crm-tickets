namespace CustomerSupport.Shared.Contracts.Messages;
public record EmailMessage(string To, string Subject, string Body);
