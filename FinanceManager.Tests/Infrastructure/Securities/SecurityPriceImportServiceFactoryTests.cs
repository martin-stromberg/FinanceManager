using FinanceManager.Application.Securities;
using FinanceManager.Infrastructure.Securities;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Infrastructure.Securities;

public sealed class SecurityPriceImportServiceFactoryTests
{
    /// <summary>
    /// Verifies that the factory returns the first matching import service.
    /// </summary>
    [Fact]
    public void Resolve_ShouldReturnMatchingService_WhenServiceCanHandleContext()
    {
        var context = new SecurityPriceImportContext("ing", "prices.csv", "text/csv");
        var matching = new Mock<ISecurityPriceImportService>();
        matching.Setup(x => x.CanHandle(context)).Returns(true);

        var sut = new SecurityPriceImportServiceFactory([matching.Object], Mock.Of<ILogger<SecurityPriceImportServiceFactory>>());

        var resolved = sut.Resolve(context);

        Assert.Same(matching.Object, resolved);
    }

    /// <summary>
    /// Verifies that the factory throws when no service supports the given context.
    /// </summary>
    [Fact]
    public void Resolve_ShouldThrow_WhenNoServiceMatchesContext()
    {
        var context = new SecurityPriceImportContext("unknown", "prices.txt", "text/plain");
        var nonMatching = new Mock<ISecurityPriceImportService>();
        nonMatching.Setup(x => x.CanHandle(context)).Returns(false);

        var sut = new SecurityPriceImportServiceFactory([nonMatching.Object], Mock.Of<ILogger<SecurityPriceImportServiceFactory>>());

        Assert.Throws<InvalidOperationException>(() => sut.Resolve(context));
    }
}
