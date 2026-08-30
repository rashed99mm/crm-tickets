namespace CustomerSupport.Domain.Common;

public sealed record FieldError(string Field, string Code, string Message);
