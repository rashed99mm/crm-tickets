namespace CustomerSupport.Domain.Entities.Verification;

/// <summary>The contact a verification is bound to. Drives which Identity confirmation flag is set.</summary>
public enum OtpVerificationType
{
    Email = 0,
    Phone = 1
}
