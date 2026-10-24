namespace FinanceManager.Tests.E2E;

/// <summary>Guards CI-compatible real-time session test configuration.</summary>
public sealed class SessionExpiryFixtureConfigurationTests
{
    /// <summary>The routine serialized suite fits the existing sixty-minute CI budget.</summary>
    [Fact]
    public void ResolveLifetimeMinutes_Default_UsesTenMinutes() => Assert.Equal(10, SessionExpiryWebAppFixture.ResolveLifetimeMinutes(null));

    /// <summary>The original production-duration reference remains explicitly selectable.</summary>
    [Fact]
    public void ResolveLifetimeMinutes_ExplicitReference_UsesThirtyMinutes() => Assert.Equal(30, SessionExpiryWebAppFixture.ResolveLifetimeMinutes("30"));

    /// <summary>Artificially short lifetimes may not reintroduce the invalid two-minute setup.</summary>
    [Fact]
    public void ResolveLifetimeMinutes_BelowTenMinutes_IsRejected() => Assert.Throws<InvalidOperationException>(() => SessionExpiryWebAppFixture.ResolveLifetimeMinutes("2"));
}
