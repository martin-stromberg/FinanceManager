using FinanceManager.Shared.Dtos.Securities;

namespace FinanceManager.Application.Securities;

/// <summary>
/// Resolves the matching provider-specific security price import service for a given file context.
/// </summary>
public interface ISecurityPriceImportServiceFactory
{
    /// <summary>
    /// Resolves an import service for the given context.
    /// </summary>
    /// <param name="context">Provider and file metadata context.</param>
    /// <returns>A matching import service implementation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no service can handle the context.</exception>
    ISecurityPriceImportService Resolve(SecurityPriceImportContext context);
}
