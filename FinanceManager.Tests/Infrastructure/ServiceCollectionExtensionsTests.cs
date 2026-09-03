using FinanceManager.Application.Attachments;
using FinanceManager.Application.Securities;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Securities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddInfrastructure("Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var scopedProvider = scope.ServiceProvider;
        Assert.NotNull(scopedProvider.GetService<IAttachmentService>());
        Assert.NotNull(scopedProvider.GetService<ISecurityPriceImportServiceFactory>());
        Assert.NotNull(scopedProvider.GetService<IDbContextFactory<AppDbContext>>());

        var importServices = scopedProvider.GetServices<ISecurityPriceImportService>().ToList();
        Assert.Contains(importServices, service => service is IngSecurityPriceImportService);
    }
}
