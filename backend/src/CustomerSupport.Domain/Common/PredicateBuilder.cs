using System.Linq.Expressions;

namespace CustomerSupport.Domain.Common;

public static class PredicateBuilder
{
    public static Expression<Func<T, bool>> True<T>() => _ => true;
    public static Expression<Func<T, bool>> False<T>() => _ => false;

    public static Expression<Func<T, bool>> And<T>(
        this Expression<Func<T, bool>> expr1,
        Expression<Func<T, bool>> expr2)
    {
        var parameter = Expression.Parameter(typeof(T));
        var body = Expression.AndAlso(
            Expression.Invoke(expr1, parameter),
            Expression.Invoke(expr2, parameter));
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    public static Expression<Func<T, bool>> Or<T>(
        this Expression<Func<T, bool>> expr1,
        Expression<Func<T, bool>> expr2)
    {
        var parameter = Expression.Parameter(typeof(T));
        var body = Expression.OrElse(
            Expression.Invoke(expr1, parameter),
            Expression.Invoke(expr2, parameter));
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    public static Expression<Func<T, bool>> WhereIf<T>(
        this Expression<Func<T, bool>> query,
        bool condition,
        Expression<Func<T, bool>> predicate)
    {
        return condition ? query.And(predicate) : query;
    }
}
