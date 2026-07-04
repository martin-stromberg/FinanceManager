using FinanceManager.Application.Securities;
using FinanceManager.Infrastructure.Securities;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

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

    [Fact]
    public void TryResolveByContent_ShouldReturnService_WhenInspectorMatches()
    {
        var context = new SecurityPriceImportContext(null, "prices.csv", "text/csv");
        var service = new InspectingService(true, new SecurityPriceImportInspectionResult("ing", "ing", "ING", "Test Security"));
        var sut = new SecurityPriceImportServiceFactory([service], Mock.Of<ILogger<SecurityPriceImportServiceFactory>>());

        var ok = sut.TryResolveByContent(context, Encoding.UTF8.GetBytes("sep=;\nZeit;Test Security\n01.07.2026 00:00:00;10,00\n"), out var resolved, out var inspection);

        Assert.True(ok);
        Assert.Same(service, resolved);
        Assert.NotNull(inspection);
        Assert.Equal("ing", inspection!.ServiceKey);
    }

    [Fact]
    public void TryResolveByContent_ShouldReturnFalse_WhenNoInspectorMatches()
    {
        var context = new SecurityPriceImportContext(null, "prices.csv", "text/csv");
        var service = new InspectingService(false, new SecurityPriceImportInspectionResult("ing", "ing", "ING", "Test Security"));
        var sut = new SecurityPriceImportServiceFactory([service], Mock.Of<ILogger<SecurityPriceImportServiceFactory>>());

        var ok = sut.TryResolveByContent(context, Encoding.UTF8.GetBytes("invalid"), out var resolved, out var inspection);

        Assert.False(ok);
        Assert.Null(resolved);
        Assert.Null(inspection);
    }

    private sealed class InspectingService : ISecurityPriceImportService, ISecurityPriceImportInspector
    {
        private readonly bool _inspectResult;
        private readonly SecurityPriceImportInspectionResult _inspectionResult;

        public InspectingService(bool inspectResult, SecurityPriceImportInspectionResult inspectionResult)
        {
            _inspectResult = inspectResult;
            _inspectionResult = inspectionResult;
        }

        public bool CanHandle(SecurityPriceImportContext context) => true;

        public Task<SecurityPriceImportResultDto> ImportAsync(Guid ownerUserId, Guid securityId, Stream stream, SecurityPriceImportContext context, CancellationToken ct)
            => Task.FromResult(new SecurityPriceImportResultDto(0, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>()));

        public bool TryInspect(SecurityPriceImportContext context, byte[] content, out SecurityPriceImportInspectionResult result)
        {
            result = _inspectionResult;
            return _inspectResult;
        }
    }
}
