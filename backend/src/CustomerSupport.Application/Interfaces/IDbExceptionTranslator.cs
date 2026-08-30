namespace CustomerSupport.Application.Interfaces;

/// <summary>
/// Translates persistence-layer exceptions into domain-meaningful outcomes
/// without the Application layer importing EF Core types.
/// </summary>
public interface IDbExceptionTranslator
{
    bool IsUniqueViolation(Exception exception);
    bool IsConcurrencyViolation(Exception exception);
}
