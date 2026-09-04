using FinanceManager.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;

namespace FinanceManager.Tests.Web;

/// <summary>
/// Tests for <see cref="DataProtectionAlphaVantageSecretProtector"/>, the ASP.NET Data Protection-backed
/// implementation used to encrypt AlphaVantage API keys at rest: round-tripping, treating blank input as
/// "no key", recognizing legacy unprotected values, and never leaking the underlying secret through an
/// exception message when a stored value fails to decrypt.
/// </summary>
public sealed class AlphaVantageSecretProtectorTests
{
    /// <summary>
    /// Verifies that protecting a key trims whitespace, produces a value prefixed with the protected-value
    /// marker and different from the plaintext, and round-trips back to the original trimmed value.
    /// </summary>
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

    /// <summary>
    /// Verifies that a whitespace-only input is treated as "no key" (protects to null), and that unprotecting
    /// a null value likewise returns null, so clearing the key field doesn't need special-case handling.
    /// </summary>
    [Fact]
    public void Protect_ShouldTreatWhitespaceAsNull()
    {
        var protector = CreateProtector();

        protector.Protect("  ").Should().BeNull();
        protector.Unprotect(null).Should().BeNull();
    }

    /// <summary>
    /// Verifies that a value without the protected-value prefix is treated as legacy plaintext: it is
    /// returned trimmed as-is, and <c>IsProtected</c> correctly reports it as not protected.
    /// </summary>
    [Fact]
    public void Unprotect_ShouldReturnLegacyPlaintextTrimmed()
    {
        var protector = CreateProtector();

        protector.Unprotect(" legacy-key ").Should().Be("legacy-key");
        protector.IsProtected(" legacy-key ").Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a value carrying the protected-value prefix but an undecryptable payload throws
    /// <see cref="AlphaVantageSecretProtectionException"/> with a fixed, generic message that does not
    /// include the original payload — preventing a stack trace or error log from leaking a partial secret.
    /// </summary>
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
