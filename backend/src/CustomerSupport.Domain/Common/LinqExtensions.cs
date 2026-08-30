using System.Linq.Expressions;
using System.Reflection;

namespace CustomerSupport.Domain.Common;

public static class LinqExtensions
{
    public static IQueryable<T> ApplyOrdering<T>(this IQueryable<T> source, string propertyPath, bool isDescending)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
            return source;

        var param = Expression.Parameter(typeof(T), "e");
        Expression? body = param;

        foreach (var member in propertyPath.Split('.'))
        {
            body = Expression.PropertyOrField(body!, member);
        }

        var lambdaType = typeof(Func<,>).MakeGenericType(typeof(T), body!.Type);
        var lambda = Expression.Lambda(lambdaType, body, param);

        var methodName = isDescending ? "OrderByDescending" : "OrderBy";

        var resultExp = Expression.Call(
            typeof(Queryable),
            methodName,
            [typeof(T), body.Type],
            source.Expression,
            Expression.Quote(lambda));

        return source.Provider.CreateQuery<T>(resultExp);
    }

}
