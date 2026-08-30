using CustomerSupport.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Services;

public class DbExceptionTranslator : IDbExceptionTranslator
{
    private const int DuplicateKeyRow = 2601;
    private const int UniqueConstraint = 2627;

    public bool IsUniqueViolation(Exception exception)
    {
        if (exception is not DbUpdateException dbEx)
            return false;

        for (Exception? inner = dbEx.InnerException; inner is not null; inner = inner.InnerException)
        {
            var number = inner.GetType().GetProperty("Number")?.GetValue(inner) as int?;
            if (number is DuplicateKeyRow or UniqueConstraint)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsConcurrencyViolation(Exception exception)
        => exception is DbUpdateConcurrencyException;
}
