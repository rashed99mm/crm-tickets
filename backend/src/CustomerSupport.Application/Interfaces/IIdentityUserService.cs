using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Application.Features.Users.Dtos;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Entities.Identity;

namespace CustomerSupport.Application.Interfaces;

public interface IIdentityUserService
{
    Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<ApplicationUser?> FindByUsernameAsync(string username, CancellationToken ct = default);
    Task<ApplicationUser?> FindByIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Resolves the portal login identity linked to a customer record (US-401). Null for a customer
    /// without a linked user — the common case for a staff-created record — which is how ticket
    /// notifications know whether a customer can even receive one.
    /// </summary>
    Task<ApplicationUser?> FindByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
    Task<bool> CheckPasswordAsync(ApplicationUser user, string password);
    Task<IReadOnlyList<string>> GetRolesAsync(ApplicationUser user);

    /// <summary>
    /// Everyone holding a role. Added for the assign picker (US-128): a supervisor needs the list
    /// of agents, and the user-administration surface is Admin-only by policy.
    /// </summary>
    Task<IReadOnlyList<ApplicationUser>> GetUsersInRoleAsync(string role, CancellationToken ct = default);
    Task<IdentityOperationResult> CreateAsync(ApplicationUser user, string password);
    Task<IdentityOperationResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword);
    Task<IdentityOperationResult> UpdateAsync(ApplicationUser user);
    Task<IdentityOperationResult> DeleteAsync(ApplicationUser user);
    Task EnsureRoleExistsAsync(string roleName, string description, CancellationToken ct = default);
    Task<IdentityOperationResult> AddToRoleAsync(ApplicationUser user, string roleName);
    Task<IdentityOperationResult> AddToRolesAsync(ApplicationUser user, IEnumerable<string> roles);
    Task<IdentityOperationResult> RemoveFromRolesAsync(ApplicationUser user, IEnumerable<string> roles);
    Task<PaginatedList<UserListItemDto>> GetUsersAsync(
        int pageIndex,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        string? search,
        bool? isActive,
        string? role,
        CancellationToken ct);
}
