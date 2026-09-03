using FinanceManager.Domain.Security;
using FluentAssertions;

namespace FinanceManager.Tests.SecurityTxtDomain;

/// <summary>Covers the invariant enforced by <see cref="SecurityTxtSettings"/>'s constructor: an expiry date for the security.txt policy must lie in the future, since a policy that has already expired would be meaningless to publish.</summary>
public sealed class SecurityTxtSettingsTests_Initialization
{
    /// <summary>Verifies the constructor rejects an Expires value in the past with an ArgumentOutOfRangeException carrying an explanatory message.</summary>
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
