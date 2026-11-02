using FinanceManager.Application.Attachments;
using FinanceManager.Application.Notifications;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Securities.GoldenCross;
using FinanceManager.Application.Securities.TrendReversal;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Securities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.Infrastructure;

/// <summary>
/// Verifies that the <c>AddInfrastructure</c> DI registration extension wires up the infrastructure layer's
/// services correctly - a smoke test that catches a forgotten or misregistered service (e.g. a missing keyed
/// import service) at test time rather than as a runtime resolution failure in the running application.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// Verifies that attachment and security price import infrastructure services are registered.
    /// </summary>
    [Fact]
    public void AddInfrastructure_ShouldRegisterAttachmentAndSecurityPriceImportServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // Ports whose implementations are provided by the web host layer.
        services.AddSingleton(Mock.Of<IPriceProvider>());
        services.AddSingleton(Mock.Of<INotificationWriter>());
        services.AddSingleton(Mock.Of<IGoldenCrossLocalizer>());
        services.AddSingleton(Mock.Of<ITrendReversalLocalizer>());

        services.AddInfrastructure("Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var scopedProvider = scope.ServiceProvider;
        Assert.NotNull(scopedProvider.GetService<IAttachmentService>());
        Assert.NotNull(scopedProvider.GetService<ISecurityPriceImportServiceFactory>());
        Assert.NotNull(scopedProvider.GetService<ISecurityPriceUpdateService>());
        Assert.NotNull(scopedProvider.GetService<IDbContextFactory<AppDbContext>>());

        var importServices = scopedProvider.GetServices<ISecurityPriceImportService>().ToList();
        Assert.Contains(importServices, service => service is IngSecurityPriceImportService);
    }
}
