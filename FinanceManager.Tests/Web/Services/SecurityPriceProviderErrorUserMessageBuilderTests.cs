using FinanceManager.Web.Services;
using FluentAssertions;

namespace FinanceManager.Tests.Web.Services;

public sealed class SecurityPriceProviderErrorUserMessageBuilderTests
{
    [Fact]
    public void Build_UnknownProviderError_ReturnsExternalErrorUserMessage()
    {
        var occurredUtc = new DateTime(2024, 3, 5, 6, 7, 0, DateTimeKind.Utc);

        var message = SecurityPriceProviderErrorUserMessageBuilder.Build(
            PriceProviderErrorClass.UnknownProviderError,
            "Unknown Security",
            "UNK-1",
            occurredUtc);

        message.Should().Contain("Unknown Security");
        message.Should().Contain("UNK-1");
        message.Should().Contain("2024-03-05 06:07 UTC");
        message.Should().Contain("externer Fehler");
    }

    [Fact]
    public void Build_UnmappedErrorClass_UsesDefaultFallbackMessage()
    {
        var occurredUtc = new DateTime(2024, 3, 5, 6, 7, 0, DateTimeKind.Utc);

        var message = SecurityPriceProviderErrorUserMessageBuilder.Build(
            (PriceProviderErrorClass)999,
            "Fallback Security",
            "FB-1",
            occurredUtc);

        message.Should().Contain("Fallback Security");
        message.Should().Contain("FB-1");
        message.Should().Contain("2024-03-05 06:07 UTC");
        message.Should().Contain("ist beim Kursabruf ein Fehler aufgetreten");
        message.Should().NotContain("externer Fehler");
    }
}
