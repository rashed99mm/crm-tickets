namespace CustomerSupport.Shared.Contracts.Messages;
public record NotificationMessage(string IdentityNumber, string TemplateCode, Dictionary<string, string> MetaData);
