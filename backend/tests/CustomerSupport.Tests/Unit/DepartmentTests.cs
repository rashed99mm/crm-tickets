using CustomerSupport.Domain.Entities.Organisation;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit;

public class DepartmentTests
{
    [Fact]
    public void Create_ValidName_IsActiveWithNoManager()
    {
        var department = Department.Create("Support", managerId: null);

        department.Name.Should().Be("Support");
        department.ManagerId.Should().BeNull();
        department.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithId_UsesTheGivenId()
    {
        var id = Guid.NewGuid();

        var department = Department.Create("Support", null, id);

        department.Id.Should().Be(id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyName_Throws(string name)
    {
        var act = () => Department.Create(name, null);

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var department = Department.Create("Support", null);

        department.Deactivate();

        department.IsActive.Should().BeFalse();
    }
}
