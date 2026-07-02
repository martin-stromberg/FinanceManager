using FinanceManager.Shared.Dtos.Securities;

namespace FinanceManager.Application.Securities;

/// <summary>
/// Defines a provider-specific service that imports security prices from an uploaded file.
/// </summary>
public interface ISecurityPriceImportService
{
    /// <summary>
    /// Determines whether the service supports the provided import context.
    /// </summary>
    /// <param name="context">Import context containing provider hint and file metadata.</param>
    /// <returns><c>true</c> when this service can process the input; otherwise <c>false</c>.</returns>
    bool CanHandle(SecurityPriceImportContext context);

    /// <summary>
    /// Imports prices from the given stream into the target security for the given owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user id used for tenant-scoped persistence.</param>
    /// <param name="securityId">Security id that receives imported prices.</param>
    /// <param name="stream">Input stream containing the uploaded price file.</param>
    /// <param name="context">Provider and file metadata context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Import statistics including counts and parsing errors.</returns>
    Task<SecurityPriceImportResultDto> ImportAsync(
        Guid ownerUserId,
        Guid securityId,
        Stream stream,
        SecurityPriceImportContext context,
        CancellationToken ct);
}
