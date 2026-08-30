using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Features.Auth.Dtos;
using Microsoft.AspNetCore.DataProtection;

namespace CustomerSupport.Infrastructure.Services;

public class DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider) : ISecretProtector
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("CustomerSupport.PlatformSettings");

    public string Protect(string value) => _protector.Protect(value);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
