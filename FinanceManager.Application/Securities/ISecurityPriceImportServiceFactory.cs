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

    /// <summary>
    /// Attempts to resolve an import service by inspecting raw file content.
    /// </summary>
    /// <param name="context">Import context containing file metadata and optional provider hint.</param>
    /// <param name="content">Raw file bytes.</param>
    /// <param name="service">Resolved import service when inspection succeeds.</param>
    /// <param name="inspection">Detected provider metadata from content inspection.</param>
    /// <returns><c>true</c> when a matching service was found; otherwise <c>false</c>.</returns>
    bool TryResolveByContent(
        SecurityPriceImportContext context,
        byte[] content,
        out ISecurityPriceImportService? service,
        out SecurityPriceImportInspectionResult? inspection);
}
