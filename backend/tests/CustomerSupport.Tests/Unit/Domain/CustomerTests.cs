using CustomerSupport.Domain.Entities.Assets;
using CustomerSupport.Domain.Entities.Customers;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>
/// Customers, notes and assets — only the invariants that carry a consequence elsewhere.
///
/// Uniqueness (AC-9) is a filtered index and the delete guard (AC-15) is a handler, so neither is
/// asserted here. What is asserted is the entity behaviour those two depend on.
/// </summary>
public class CustomerTests
{
    private static readonly Guid Author = Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>
    /// Not cosmetic. <c>UX_Customers_Email</c> is a plain unique index, so it treats
    /// <c>Layla@x.com</c> and <c>layla@x.com</c> as different values — the duplicate-email conflict
    /// (AC-9) is only reliable because the entity normalises before the index ever sees it.
    /// </summary>
    [Fact]
    [Trait("AC", "9")]
    public void AC9_Email_Is_Lowercased_So_The_Unique_Index_Catches_Case_Variants()
    {
        Customer.Create("Layla", " LAYLA@Example.COM ", null).Email.Should().Be("layla@example.com");
    }

    [Theory]
    [Trait("AC", "8")]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("@example.com")]
    [InlineData("spaces in@example.com")]
    public void AC8_Create_Rejects_A_Malformed_Email(string email)
    {
        var act = () => Customer.Create("Layla", email, null);

        act.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    [Fact]
    [Trait("AC", "14")]
    public void AC14_Updating_Applies_The_Same_Validation_As_Creating()
    {
        var customer = Customer.Create("Layla", "layla@example.com", null);

        var act = () => customer.Update("Layla", "not-an-email", null);

        act.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    /// <summary>
    /// AC-19's enforcement is at the handler, which reads the token. The entity's contribution is
    /// having no shape that expresses a note without an author — no setter, no default.
    /// </summary>
    [Fact]
    [Trait("AC", "19")]
    public void AC19_A_Note_Cannot_Exist_Without_An_Author()
    {
        var act = () => CustomerNote.Create(Guid.NewGuid(), "Called back, awaiting logs.", Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("authorId");
    }

    /// <summary>
    /// The traversal defence (AC-25). The hostile name survives as metadata — it is what the
    /// customer will see — but the name written to disk is server-generated, so there is no
    /// sanitiser to get wrong.
    /// </summary>
    [Theory]
    [Trait("AC", "25")]
    [InlineData("../../etc/passwd")]
    [InlineData(@"..\..\windows\system32\config\sam")]
    [InlineData("quarterly report.pdf")]
    public void AC25_The_Stored_Name_Is_Server_Generated_And_Cannot_Escape_The_Directory(string originalFileName)
    {
        var asset = Asset.Create(originalFileName, "application/pdf", 1024, Author);

        asset.OriginalFileName.Should().Be(originalFileName);
        asset.StoredFileName.Should().NotContainAny("/", "\\", "..");
        Path.GetFileName(asset.StoredFileName).Should().Be(asset.StoredFileName);
    }
}
