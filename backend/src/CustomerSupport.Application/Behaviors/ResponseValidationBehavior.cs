using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Application.Localization;
using CustomerSupport.Domain.Common;
using FluentValidation;
using MediatR;

namespace CustomerSupport.Application.Behaviors;

public class ResponseValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    IMessageFactory messageFactory,
    ILocalizationService localization)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(e => e != null).ToList();

        if (failures.Count == 0) return await next();

        var fieldErrors = failures
            .GroupBy(f => f.PropertyName)
            .Select(g => new FieldError(
                g.Key,
                SystemCodeMap.Resolve(g.Key),
                localization.GetStringOrDefault(g.First().ErrorCode, g.First().ErrorMessage)))
            .ToList();

        var responseType = typeof(TResponse);
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Response<>))
        {
            var innerType = responseType.GetGenericArguments()[0];
            var method = typeof(IMessageFactory)
                .GetMethod(nameof(IMessageFactory.Validation))!
                .MakeGenericMethod(innerType);
            return (TResponse)method.Invoke(messageFactory, [ApplicationErrors.General.VALIDATION_ERROR, fieldErrors])!;
        }

        return (TResponse)(object)messageFactory.Validation<TResponse>(ApplicationErrors.General.VALIDATION_ERROR, fieldErrors);
    }
}
