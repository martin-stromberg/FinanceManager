using FinanceManager.Domain.Security;
using FluentAssertions;

namespace FinanceManager.Tests.SecurityTxtDomain;

public sealed class SecurityTxtSettingsTests_Initialization
{
    [Fact]
    public void Constructor_Throws_WhenExpiresIsNotInFuture()
    {
        var act = () => new SecurityTxtSettings(
            "mailto:security@example.com",
            DateTimeOffset.UtcNow.AddMinutes(-1));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Expires must be in the future.*");
    }
}
