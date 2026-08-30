using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Seeders;

public class IdentitySeeder
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public IdentitySeeder(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        AppDbContext db)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _db = db;
    }

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedDefaultAdminAsync();
        await SeedRoleUsersAsync();
    }

    private async Task SeedRolesAsync()
    {
        var roles = new[]
        {
            (ApplicationRole.Roles.SuperAdmin, "Super Administrator with full access"),
            (ApplicationRole.Roles.Admin, "Administrator with elevated permissions"),
            (ApplicationRole.Roles.ContentManager, "Manages content on the platform"),
            (ApplicationRole.Roles.StateRepresentative, "State government representative"),
            (ApplicationRole.Roles.User, "Regular user with basic access"),
            (ApplicationRole.Roles.Visitor, "Guest visitor with limited access"),
            (ApplicationRole.Roles.Agent, "Support agent who works assigned tickets"),
            (ApplicationRole.Roles.Supervisor, "Support supervisor who assigns and reassigns tickets")
        };

        foreach (var (roleName, description) in roles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var role = ApplicationRole.Create(roleName, description);
                await _roleManager.CreateAsync(role);
            }
        }
    }

    private async Task SeedDefaultAdminAsync()
    {
        var adminEmail = "admin@cce-platform.com";
        var adminUser = await _userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = ApplicationUser.Create(
                adminEmail,
                "admin",
                "System",
                "Administrator"
            );

            var result = await _userManager.CreateAsync(adminUser, "Admin@123456");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(adminUser, ApplicationRole.Roles.SuperAdmin);
                await _userManager.AddToRoleAsync(adminUser, ApplicationRole.Roles.Admin);

                adminUser.AssignOrganisation(
                    DepartmentBranchSeeder.DefaultDepartmentId,
                    DepartmentBranchSeeder.DefaultBranchId,
                    TeamSeeder.DefaultTeamId);
                await _userManager.UpdateAsync(adminUser);
            }
        }
    }

    private async Task SeedRoleUsersAsync()
    {
        // These accounts are local development fixtures, not production identities. Keeping them
        // idempotent makes restarting the API safe while still making every role testable.
        var users = new[]
        {
            (Email: "superadmin@support.local", Username: "superadmin", Role: ApplicationRole.Roles.SuperAdmin),
            (Email: "admin@support.local", Username: "admin.support", Role: ApplicationRole.Roles.Admin),
            (Email: "content.manager@support.local", Username: "content.manager", Role: ApplicationRole.Roles.ContentManager),
            (Email: "state.representative@support.local", Username: "state.representative", Role: ApplicationRole.Roles.StateRepresentative),
            (Email: "user@support.local", Username: "portal.user", Role: ApplicationRole.Roles.User),
            (Email: "visitor@support.local", Username: "visitor", Role: ApplicationRole.Roles.Visitor),
            (Email: "agent@support.local", Username: "agent", Role: ApplicationRole.Roles.Agent),
            (Email: "supervisor@support.local", Username: "supervisor", Role: ApplicationRole.Roles.Supervisor),
        };

        foreach (var fixture in users)
        {
            var user = await _userManager.FindByEmailAsync(fixture.Email);
            if (user is null)
            {
                user = ApplicationUser.Create(fixture.Email, fixture.Username, "Demo", fixture.Role);
                if (fixture.Role == ApplicationRole.Roles.User)
                {
                    var customer = await EnsurePortalCustomerAsync(fixture.Email);
                    user.LinkCustomer(customer.Id);
                }

                var created = await _userManager.CreateAsync(user, "Support@123456");
                if (!created.Succeeded)
                {
                    continue;
                }
            }

            if (fixture.Role == ApplicationRole.Roles.User && user.CustomerId is null)
            {
                var customer = await EnsurePortalCustomerAsync(fixture.Email);
                user.LinkCustomer(customer.Id);
                await _userManager.UpdateAsync(user);
            }

            if (!await _userManager.IsInRoleAsync(user, fixture.Role))
            {
                await _userManager.AddToRoleAsync(user, fixture.Role);
            }
        }
    }

    private async Task<Customer> EnsurePortalCustomerAsync(string email)
    {
        var normalizedEmail = email.ToLowerInvariant();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == normalizedEmail);
        if (customer is not null)
        {
            return customer;
        }

        customer = Customer.Create("Demo Portal User", email, "+966500000000");
        await _db.Customers.AddAsync(customer);
        return customer;
    }
}
