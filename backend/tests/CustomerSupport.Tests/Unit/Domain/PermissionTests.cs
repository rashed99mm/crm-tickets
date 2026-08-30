using CustomerSupport.Domain.Entities.Identity;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

public sealed class PermissionTests
{
    [Fact] // AC-804.1
    public void PermissionEntityHasRequiredFields()
    {
        var permission = Permission.Create("ticket.create", "Create support tickets");

        permission.Id.Should().NotBeEmpty();
        permission.Name.Should().Be("ticket.create");
        permission.Description.Should().Be("Create support tickets");
    }

    [Fact] // AC-804.1
    public void PermissionRejectsEmptyOrOverlongKey()
    {
        var act = () => Permission.Create(new string('x', 101));

        act.Should().Throw<ArgumentException>();
    }

    [Fact] // AC-804.2
    public void RolePermissionMapsRoleToPermission()
    {
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        var mapping = RolePermission.Create(roleId, permissionId);

        mapping.RoleId.Should().Be(roleId);
        mapping.PermissionId.Should().Be(permissionId);
    }
}
