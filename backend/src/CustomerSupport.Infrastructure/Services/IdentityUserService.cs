using CustomerSupport.Application.Features.Users.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Services;

public class IdentityUserService : IIdentityUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly AppDbContext _dbContext;

    public IdentityUserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        AppDbContext dbContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
    }

    public Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        _userManager.FindByEmailAsync(email);

    public Task<ApplicationUser?> FindByUsernameAsync(string username, CancellationToken ct = default) =>
        _userManager.FindByNameAsync(username);

    public Task<ApplicationUser?> FindByIdAsync(Guid userId, CancellationToken ct = default) =>
        _userManager.FindByIdAsync(userId.ToString());

    public Task<ApplicationUser?> FindByCustomerIdAsync(Guid customerId, CancellationToken ct = default) =>
        _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.CustomerId == customerId, ct);

    public Task<bool> CheckPasswordAsync(ApplicationUser user, string password) =>
        _userManager.CheckPasswordAsync(user, password);

    public async Task<IReadOnlyList<string>> GetRolesAsync(ApplicationUser user) =>
        (await _userManager.GetRolesAsync(user)).ToList();

    public async Task<IReadOnlyList<ApplicationUser>> GetUsersInRoleAsync(string role, CancellationToken ct = default)
    {
        // Active users only: a deactivated account is not someone work can be handed to, and
        // offering one in the picker would produce an assignment nobody ever works.
        var users = await _userManager.GetUsersInRoleAsync(role);
        return [.. users.Where(u => u.IsActive).OrderBy(u => u.FirstName).ThenBy(u => u.LastName)];
    }

    public async Task<IdentityOperationResult> CreateAsync(ApplicationUser user, string password)
    {
        var result = await _userManager.CreateAsync(user, password);
        return ToOperationResult(result);
    }

    public async Task<IdentityOperationResult> ChangePasswordAsync(
        ApplicationUser user, string currentPassword, string newPassword)
    {
        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return ToOperationResult(result);
    }

    public async Task<IdentityOperationResult> UpdateAsync(ApplicationUser user)
    {
        var result = await _userManager.UpdateAsync(user);
        return ToOperationResult(result);
    }

    public async Task<IdentityOperationResult> DeleteAsync(ApplicationUser user)
    {
        var result = await _userManager.DeleteAsync(user);
        return ToOperationResult(result);
    }

    public async Task EnsureRoleExistsAsync(string roleName, string description, CancellationToken ct = default)
    {
        var roleExists = await _roleManager.RoleExistsAsync(roleName);
        if (!roleExists)
        {
            var role = ApplicationRole.Create(roleName, description);
            await _roleManager.CreateAsync(role);
        }
    }

    public async Task<IdentityOperationResult> AddToRoleAsync(ApplicationUser user, string roleName)
    {
        var result = await _userManager.AddToRoleAsync(user, roleName);
        return ToOperationResult(result);
    }

    public async Task<IdentityOperationResult> AddToRolesAsync(ApplicationUser user, IEnumerable<string> roles)
    {
        var roleList = roles.ToList();
        var result = await _userManager.AddToRolesAsync(user, roleList);
        return ToOperationResult(result);
    }

    public async Task<IdentityOperationResult> RemoveFromRolesAsync(ApplicationUser user, IEnumerable<string> roles)
    {
        var roleList = roles.ToList();
        var result = await _userManager.RemoveFromRolesAsync(user, roleList);
        return ToOperationResult(result);
    }

    public async Task<PaginatedList<UserListItemDto>> GetUsersAsync(
        int pageIndex,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        string? search,
        bool? isActive,
        string? role,
        CancellationToken ct)
    {
        var query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(u =>
                u.Email!.ToLower().Contains(searchLower) ||
                u.UserName!.ToLower().Contains(searchLower) ||
                u.FirstName.ToLower().Contains(searchLower) ||
                u.LastName.ToLower().Contains(searchLower));
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            // Filtering by role membership for a server-paged list: a client-filtered "agents"
            // tab would only narrow the page it already fetched. Resolve the role to its member
            // ids, then keep users whose id is among them.
            var roleIds = _dbContext.Roles
                .Where(r => r.NormalizedName == role.Trim().ToUpper())
                .Select(r => r.Id)
                .ToList();
            var memberIds = _dbContext.UserRoles
                .Where(ur => roleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .ToList();
            query = query.Where(u => memberIds.Contains(u.Id));
        }

        query = sortBy?.ToLower() switch
        {
            "email" => sortDirection == "desc" ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "username" => sortDirection == "desc" ? query.OrderByDescending(u => u.UserName) : query.OrderBy(u => u.UserName),
            "firstname" => sortDirection == "desc" ? query.OrderByDescending(u => u.FirstName) : query.OrderBy(u => u.FirstName),
            "lastname" => sortDirection == "desc" ? query.OrderByDescending(u => u.LastName) : query.OrderBy(u => u.LastName),
            "createdat" => sortDirection == "desc" ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
            "lastlogin" => sortDirection == "desc" ? query.OrderByDescending(u => u.LastLoginAt) : query.OrderBy(u => u.LastLoginAt),
            _ => query.OrderBy(u => u.CreatedAt)
        };

        var totalCount = await query.CountAsync(ct);
        var boundedPageSize = Math.Min(pageSize, 50);
        var boundedPageIndex = Math.Max(pageIndex, 1);
        var skip = (boundedPageIndex - 1) * boundedPageSize;

        var users = await query
            .Skip(skip)
            .Take(boundedPageSize)
            .ToListAsync(ct);

        if (users.Count == 0)
        {
            return PaginatedList<UserListItemDto>.Create(
                new List<UserListItemDto>(), totalCount, boundedPageIndex, boundedPageSize);
        }

        var userIds = users.Select(u => u.Id).ToList();
        var userRoles = await _dbContext.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_dbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name! })
            .ToListAsync(ct);

        var rolesLookup = userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

        var userDtos = users.Select(user => new UserListItemDto(
            user.Id,
            user.Email!,
            user.UserName!,
            user.FirstName,
            user.LastName,
            user.IsActive,
            user.CreatedAt,
            rolesLookup.GetValueOrDefault(user.Id, new List<string>())
        )).ToList();

        return PaginatedList<UserListItemDto>.Create(userDtos, totalCount, boundedPageIndex, boundedPageSize);
    }

    private static IdentityOperationResult ToOperationResult(IdentityResult result) =>
        result.Succeeded
            ? IdentityOperationResult.Success()
            : IdentityOperationResult.Failure(
                result.Errors.Select(error => (error.Code, error.Description)));
}
