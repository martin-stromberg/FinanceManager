namespace FinanceManager.Application.Setup;

/// <summary>
/// Service for importing initial setup data (e.g. from a previously exported configuration).
/// </summary>
public interface ISetupImportService
{
    /// <summary>
    /// Imports a setup file for the specified user.
    /// </summary>
    /// <param name="userId">User identifier that owns the imported data.</param>
    /// <param name="fileStream">Stream containing the import file content.</param>
    /// <param name="replaceExisting">When true, existing data may be replaced by the import.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ImportAsync(Guid userId, Stream fileStream, bool replaceExisting, CancellationToken ct);
}