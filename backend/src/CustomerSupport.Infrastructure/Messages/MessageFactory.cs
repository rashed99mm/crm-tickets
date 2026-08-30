using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Localization;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Infrastructure.Messages;

public class MessageFactory : IMessageFactory
{
    private readonly ILocalizationService _localization;

    public MessageFactory(ILocalizationService localization)
    {
        _localization = localization;
    }

    public Response<T> Success<T>(T data, string domainKey)
    {
        var code = SystemCodeMap.Resolve(domainKey);
        var message = _localization.GetString(domainKey);
        return Response<T>.Ok(data, code, message);
    }

    public Response<T> Fail<T>(string domainKey, MessageType type)
    {
        var code = SystemCodeMap.Resolve(domainKey);
        var message = _localization.GetString(domainKey);
        return Response<T>.Fail(code, message, type);
    }

    public Response<T> Fail<T>(string domainKey, MessageType type, IList<FieldError> errors)
    {
        var code = SystemCodeMap.Resolve(domainKey);
        var message = _localization.GetString(domainKey);
        return Response<T>.Fail(code, message, type, errors);
    }

    public Response<T> NotFound<T>(string domainKey)
    {
        return Fail<T>(domainKey, MessageType.NotFound);
    }

    public Response<T> Validation<T>(string domainKey, IList<FieldError> errors)
    {
        var code = SystemCodeMap.Resolve(domainKey);
        var message = _localization.GetString(domainKey);
        return Response<T>.Fail(code, message, MessageType.Validation, errors);
    }
}
