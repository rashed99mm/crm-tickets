using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Messages;

public interface IMessageFactory
{
    Response<T> Success<T>(T data, string domainKey);
    Response<T> Fail<T>(string domainKey, MessageType type);
    Response<T> Fail<T>(string domainKey, MessageType type, IList<FieldError> errors);
    Response<T> NotFound<T>(string domainKey);
    Response<T> Validation<T>(string domainKey, IList<FieldError> errors);
}
