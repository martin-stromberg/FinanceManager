using FinanceManager.Web.Components.Pages.Setup;
using FluentAssertions;

namespace FinanceManager.Tests.Components;

public sealed class SetupUpdateTabTests
{
    [Fact]
    public void ShouldReloadAfterHealth_RequiresObservedOutage()
    {
        SetupUpdateTab.ShouldReloadAfterHealth(outageObserved: false, healthSuccessful: true).Should().BeFalse();
        SetupUpdateTab.ShouldReloadAfterHealth(outageObserved: true, healthSuccessful: false).Should().BeFalse();
        SetupUpdateTab.ShouldReloadAfterHealth(outageObserved: true, healthSuccessful: true).Should().BeTrue();
    }
}
