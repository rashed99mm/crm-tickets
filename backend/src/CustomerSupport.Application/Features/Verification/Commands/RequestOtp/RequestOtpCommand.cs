using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Verification.Dtos;
using CustomerSupport.Domain.Entities.Verification;
using MediatR;

namespace CustomerSupport.Application.Features.Verification.Commands.RequestOtp;

/// <summary>
/// Requests a fresh six-digit code for the caller's own contact (Email or Phone). Dispatch happens
/// through the notification gateway on the channel that matches <see cref="Type"/>; the hashed code
/// is persisted only after the gateway accepts the dispatch (OTP-9), and the response carries the
/// verification id needed by the verify endpoint (OTP-1, OTP-2, OTP-3).
/// </summary>
public record RequestOtpCommand(string Contact, OtpVerificationType Type)
    : ICommand<Response<RequestOtpResponse>>;