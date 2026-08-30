namespace CustomerSupport.Application.Features.Customers.Dtos;

/// <summary>A customer as the API returns it.</summary>
public record CustomerDto(Guid Id, string Name, string Email, string? Phone, DateTime CreatedAt);
