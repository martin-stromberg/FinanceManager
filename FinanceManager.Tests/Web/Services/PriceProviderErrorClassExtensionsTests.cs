using FinanceManager.Web.Services;
using FluentAssertions;

namespace FinanceManager.Tests.Web.Services;

public sealed class PriceProviderErrorClassExtensionsTests
{
    [Theory]
    [InlineData(PriceProviderErrorClass.InvalidSymbolOrFunction, "INVALID_SYMBOL_OR_FUNCTION")]
    [InlineData(PriceProviderErrorClass.RateLimit, "RATE_LIMIT")]
    [InlineData(PriceProviderErrorClass.TransientNetwork, "TRANSIENT_NETWORK")]
    public void ToCode_ReturnsStableCode_ForKnownValues(PriceProviderErrorClass errorClass, string expectedCode)
    {
        errorClass.ToCode().Should().Be(expectedCode);
    }

    [Fact]
    public void ToCode_ReturnsUnknownProviderError_ForUnknownValue()
    {
        ((PriceProviderErrorClass)999).ToCode().Should().Be("UNKNOWN_PROVIDER_ERROR");
    }
}
