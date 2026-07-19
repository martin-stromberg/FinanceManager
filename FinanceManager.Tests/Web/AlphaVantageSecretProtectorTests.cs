using FinanceManager.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;

namespace FinanceManager.Tests.Web;

public sealed class AlphaVantageSecretProtectorTests
{
    [Fact]
    public void Protect_ShouldReturnPrefixedValueDifferentFromPlaintext()
    {
        var protector = CreateProtector();

        var stored = protector.Protect(" demo-key ");

        stored.Should().NotBeNull();
        stored.Should().StartWith(DataProtectionAlphaVantageSecretProtector.ProtectedPrefix);
        stored.Should().NotBe("demo-key");
        protector.Unprotect(stored).Should().Be("demo-key");
        protector.IsProtected(stored).Should().BeTrue();
    }

    [Fact]
    public void Protect_ShouldTreatWhitespaceAsNull()
    {
        var protector = CreateProtector();

        protector.Protect("  ").Should().BeNull();
        protector.Unprotect(null).Should().BeNull();
    }

    [Fact]
    public void Unprotect_ShouldReturnLegacyPlaintextTrimmed()
    {
        var protector = CreateProtector();

        protector.Unprotect(" legacy-key ").Should().Be("legacy-key");
        protector.IsProtected(" legacy-key ").Should().BeFalse();
    }

    [Fact]
    public void Unprotect_InvalidProtectedValue_ShouldThrowGenericMessageWithoutSecret()
    {
        var protector = CreateProtector();
        const string secretPayload = "secret-key";

        var act = () => protector.Unprotect(DataProtectionAlphaVantageSecretProtector.ProtectedPrefix + secretPayload);

        act.Should().Throw<AlphaVantageSecretProtectionException>()
            .WithMessage("Stored AlphaVantage API key cannot be read.")
            .Which.Message.Should().NotContain(secretPayload);
    }

    private static DataProtectionAlphaVantageSecretProtector CreateProtector()
        => new(DataProtectionProvider.Create("FinanceManager.Tests"));
}
