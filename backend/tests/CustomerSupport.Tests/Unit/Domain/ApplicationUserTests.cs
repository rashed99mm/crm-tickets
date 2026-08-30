using CustomerSupport.Domain.Entities.Identity;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>
/// <see cref="ApplicationUser"/> customer linkage (US-401, PJ-2): portal registrations link one
/// customer, staff never. The JWT <c>customerId</c> claim (PJ-3) is only issued when this is set.
/// </summary>
public class ApplicationUserTests
{
    private static ApplicationUser CreateUser() =>
        ApplicationUser.Create("customer@example.com", "customer@example.com", "Layla", "Haddad");

    [Fact]
    [Trait("AC", "401")]
    public void PJ2_LinkCustomer_Sets_The_Link()
    {
        var user = CreateUser();
        var customerId = Guid.NewGuid();

        user.LinkCustomer(customerId);

        user.CustomerId.Should().Be(customerId);
    }

    [Fact]
    [Trait("AC", "401")]
    public void PJ2_LinkCustomer_Empty_Throws()
    {
        var user = CreateUser();

        var act = () => user.LinkCustomer(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "401")]
    public void PJ2_Default_User_Has_No_Customer_Link()
    {
        CreateUser().CustomerId.Should().BeNull();
    }
}