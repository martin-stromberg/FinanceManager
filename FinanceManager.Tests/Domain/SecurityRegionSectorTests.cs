using FinanceManager.Domain.Securities;
using FluentAssertions;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Pure domain-layer unit tests for the <see cref="Security.Region"/> and <see cref="Security.Sector"/>
/// length validation performed by <see cref="Security.Update"/>. No infrastructure dependencies –
/// all assertions operate on in-memory objects only.
/// </summary>
public sealed class SecurityRegionSectorTests
{
    private static readonly Guid OwnerUserId = Guid.NewGuid();

    private static Security CreateSecurity(string? region = null, string? sector = null)
        => new(OwnerUserId, "Test Security", "ISIN123", null, null, "EUR", null, region, sector);

    /// <summary>
    /// A region of exactly 255 characters is accepted and stored as-is (trimmed).
    /// </summary>
    [Fact]
    public void Update_ShouldAcceptRegion_WhenExactly255Characters()
    {
        var region = new string('A', 255);
        var security = CreateSecurity();

        security.Update("Test Security", "ISIN123", null, null, "EUR", null, region, null);

        security.Region.Should().Be(region);
    }

    /// <summary>
    /// A region longer than 255 characters is rejected with an <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Update_ShouldThrow_WhenRegionExceeds255Characters()
    {
        var region = new string('A', 256);
        var security = CreateSecurity();

        var act = () => security.Update("Test Security", "ISIN123", null, null, "EUR", null, region, null);

        act.Should().Throw<ArgumentException>().WithParameterName("region");
    }

    /// <summary>
    /// A sector of exactly 255 characters is accepted and stored as-is (trimmed).
    /// </summary>
    [Fact]
    public void Update_ShouldAcceptSector_WhenExactly255Characters()
    {
        var sector = new string('B', 255);
        var security = CreateSecurity();

        security.Update("Test Security", "ISIN123", null, null, "EUR", null, null, sector);

        security.Sector.Should().Be(sector);
    }

    /// <summary>
    /// A sector longer than 255 characters is rejected with an <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Update_ShouldThrow_WhenSectorExceeds255Characters()
    {
        var sector = new string('B', 256);
        var security = CreateSecurity();

        var act = () => security.Update("Test Security", "ISIN123", null, null, "EUR", null, null, sector);

        act.Should().Throw<ArgumentException>().WithParameterName("sector");
    }

    /// <summary>
    /// Null region and sector remain valid (both are optional fields).
    /// </summary>
    [Fact]
    public void Update_ShouldAllowNullRegionAndSector()
    {
        var security = CreateSecurity("Europa", "Technologie");

        security.Update("Test Security", "ISIN123", null, null, "EUR", null, null, null);

        security.Region.Should().BeNull();
        security.Sector.Should().BeNull();
    }
}
