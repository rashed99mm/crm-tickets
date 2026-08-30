using CustomerSupport.Domain.Entities.Organisation;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit;

public class BranchTests
{
    [Fact]
    public void Create_NoTimezoneGiven_DefaultsToUtc()
    {
        var branch = Branch.Create("Head Office", region: null, timezone: null);

        branch.Timezone.Should().Be("UTC");
        branch.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithId_UsesTheGivenId()
    {
        var id = Guid.NewGuid();

        var branch = Branch.Create("Head Office", null, "UTC", id);

        branch.Id.Should().Be(id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyName_Throws(string name)
    {
        var act = () => Branch.Create(name, null, "UTC");

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var branch = Branch.Create("Head Office", null, "UTC");

        branch.Deactivate();

        branch.IsActive.Should().BeFalse();
    }
}
