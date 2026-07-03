using FinanceManager.Shared.Dtos.Securities;

namespace FinanceManager.Application.Securities;

/// <summary>
/// Performs provider-specific content inspection for security price import files.
/// </summary>
public interface ISecurityPriceImportInspector
{
    /// <summary>
    /// Inspects raw file content and returns import metadata when the format matches.
    /// </summary>
    /// <param name="context">Import context containing file metadata and optional provider hint.</param>
    /// <param name="content">Raw file bytes.</param>
    /// <param name="result">Detected provider and service metadata.</param>
    /// <returns><c>true</c> when this inspector recognizes the file format; otherwise <c>false</c>.</returns>
    bool TryInspect(SecurityPriceImportContext context, byte[] content, out SecurityPriceImportInspectionResult result);
}
