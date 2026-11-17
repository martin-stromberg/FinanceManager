using FinanceManager.Shared.Dtos.Mcp;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// Abstraction for reading and updating the database-stored, administrator-editable MCP
/// settings (kill switch and write-stop lid). Mirrors the
/// <c>IWellKnownSettingsService</c> singleton-row pattern: the read path never writes and
/// falls back to the safe defaults (<see langword="false"/>) when no row exists yet.
/// </summary>
public interface IMcpSettingsService
{
    /// <summary>Returns the configured MCP settings for admin editing (default fallback, no write).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result.</returns>
    Task<McpSettingsDto> GetAsync(CancellationToken ct);

    /// <summary>Persists the configured MCP settings (admin path; materializes the singleton row on first write).</summary>
    /// <param name="request">Request payload.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(McpSettingsUpdateRequest request, CancellationToken ct);

    /// <summary>Returns whether the MCP endpoint is enabled (default <see langword="false"/> when unconfigured).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result.</returns>
    Task<bool> IsEnabledAsync(CancellationToken ct);

    /// <summary>Returns whether write-class tools may run (default <see langword="false"/> when unconfigured).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result.</returns>
    Task<bool> IsWriteAccessEnabledAsync(CancellationToken ct);
}
