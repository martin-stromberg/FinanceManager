namespace FinanceManager.Tests.E2E;

/// <summary>Guards CI-compatible real-time session test configuration.</summary>
public sealed class SessionExpiryFixtureConfigurationTests
{
    /// <summary>The routine serialized suite fits the existing sixty-minute CI budget.</summary>
    [Fact]
    public void ResolveLifetimeMinutes_Default_UsesThreeMinutes() => Assert.Equal(3, SessionExpiryWebAppFixture.ResolveLifetimeMinutes(null));

    /// <summary>The original production-duration reference remains explicitly selectable.</summary>
    [Fact]
    public void ResolveLifetimeMinutes_ExplicitReference_UsesThirtyMinutes() => Assert.Equal(30, SessionExpiryWebAppFixture.ResolveLifetimeMinutes("30"));

    /// <summary>Shortened timing must retain enough distance from the browser activity throttle.</summary>
    [Fact]
    public void ResolveLifetimeMinutes_BelowThreeMinutes_IsRejected() => Assert.Throws<InvalidOperationException>(() => SessionExpiryWebAppFixture.ResolveLifetimeMinutes("2"));

    /// <summary>The shortened host scales the minimum; the production-duration reference preserves the production minimum.</summary>
    /// <param name="lifetimeMinutes">The requested token lifetime.</param>
    /// <param name="expectedMinimum">The host's expected minimum renewal window.</param>
    [Theory]
    [InlineData(3, 1)]
    [InlineData(10, 5)]
    [InlineData(30, 5)]
    public void ResolveMinimumRenewalWindowMinutes_PreservesTiming(int lifetimeMinutes, int expectedMinimum)
        => Assert.Equal(expectedMinimum, SessionExpiryWebAppFixture.ResolveMinimumRenewalWindowMinutes(lifetimeMinutes));
}
