using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region Aggregates

    /// <summary>
    /// Starts an aggregates rebuild background task. Returns the status object describing the started task.
    /// </summary>
    /// <param name="allowDuplicate">When true, allows enqueuing a duplicate task even if one is already running.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Status of the rebuild task.</returns>
    public async Task<AggregatesRebuildStatusDto> Aggregates_RebuildAsync(bool allowDuplicate = false, CancellationToken ct = default)
    {
        var url = $"/api/background-tasks/aggregates/rebuild?allowDuplicate={(allowDuplicate ? "true" : "false")}";
        var resp = await _http.PostAsync(url, content: null, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<AggregatesRebuildStatusDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Gets the current status of the aggregates rebuild background task.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Current rebuild status.</returns>
    public async Task<AggregatesRebuildStatusDto> Aggregates_GetRebuildStatusAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/background-tasks/aggregates/rebuild/status", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<AggregatesRebuildStatusDto>(cancellationToken: ct))!;
    }

    #endregion Aggregates
}