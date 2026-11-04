using FinanceManager.Shared.Dtos.Mcp;
using System.Net.Http.Json;
using System.Text;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region MCP Audit

    /// <summary>
    /// Lists the audit entries of the current user (newest first). All optional filters
    /// are combined with AND semantics on the server; the read is strictly owner-scoped.
    /// </summary>
    /// <param name="from">Optional inclusive lower bound (UTC).</param>
    /// <param name="to">Optional inclusive upper bound (UTC).</param>
    /// <param name="category">Optional category (McpAuditCategory as short).</param>
    /// <param name="mcpAccessId">Optional access restriction.</param>
    /// <param name="correlationId">Optional correlation id.</param>
    /// <param name="toolName">Optional exact <c>fm_*</c> tool name.</param>
    /// <param name="result">Optional exact result code.</param>
    /// <param name="skip">Items to skip.</param>
    /// <param name="take">Items to take.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The list of audit list items.</returns>
    public async Task<IReadOnlyList<McpAuditEntryListItemDto>> Mcp_ListAuditEntriesAsync(
        DateTime? from = null, DateTime? to = null, short? category = null,
        Guid? mcpAccessId = null, Guid? correlationId = null, string? toolName = null,
        string? result = null, int skip = 0, int take = 100, CancellationToken ct = default)
    {
        var url = "/api/mcp-audit" + BuildAuditQuery(from, to, category, mcpAccessId, correlationId, toolName, result, skip, take);
        var resp = await _http.GetAsync(url, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<McpAuditEntryListItemDto>>(cancellationToken: ct)
            ?? Array.Empty<McpAuditEntryListItemDto>();
    }

    /// <summary>
    /// Gets one audit entry detail including payload and field deltas, or null when not found.
    /// </summary>
    /// <param name="id">Entry id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The detail DTO or null.</returns>
    public async Task<McpAuditEntryDetailDto?> Mcp_GetAuditEntryAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/mcp-audit/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<McpAuditEntryDetailDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Verifies the integrity of the own audit hash chain (monotonic gap-free sequence,
    /// intact predecessor linkage, valid entry hashes).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The verification result.</returns>
    public async Task<McpAuditVerifyResultDto> Mcp_VerifyAuditChainAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/mcp-audit/verify", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<McpAuditVerifyResultDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Loads the usage overview of one own access aggregated from the audit trail
    /// (F-0603), or null when the access does not exist/belong to the user.
    /// </summary>
    /// <param name="accessId">Access id.</param>
    /// <param name="recent">Optional size of the recent-calls excerpt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The usage DTO or null.</returns>
    public async Task<McpAccessUsageDto?> Mcp_GetAccessUsageAsync(Guid accessId, int recent = 10, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/mcp-audit/usage/{accessId}?recent={recent}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<McpAccessUsageDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Builds the export download URL of the own audit protocol. The download runs via
    /// browser navigation/anchor (cookie auth) — the same mechanism the statement-draft
    /// exports use (<c>financeManager.downloadFromUrl</c>).
    /// </summary>
    /// <param name="format"><c>csv</c> or <c>json</c>.</param>
    /// <param name="from">Optional inclusive lower bound (UTC).</param>
    /// <param name="to">Optional inclusive upper bound (UTC).</param>
    /// <param name="category">Optional category (McpAuditCategory as short).</param>
    /// <param name="mcpAccessId">Optional access restriction.</param>
    /// <param name="correlationId">Optional correlation id.</param>
    /// <param name="toolName">Optional exact tool name.</param>
    /// <param name="result">Optional exact result code.</param>
    /// <returns>The relative export URL.</returns>
    public string Mcp_BuildAuditExportUrl(string format,
        DateTime? from = null, DateTime? to = null, short? category = null,
        Guid? mcpAccessId = null, Guid? correlationId = null, string? toolName = null,
        string? result = null)
    {
        var qs = new StringBuilder("/api/mcp-audit/export?format=" + Uri.EscapeDataString(format ?? "json"));
        AppendAuditQuery(qs, from, to, category, mcpAccessId, correlationId, toolName, result, null, null);
        return qs.ToString();
    }

    /// <summary>Shared query-string builder of the audit list endpoint (leads with "?").</summary>
    private static string BuildAuditQuery(
        DateTime? from, DateTime? to, short? category, Guid? mcpAccessId,
        Guid? correlationId, string? toolName, string? result, int? skip, int? take)
    {
        var sb = new StringBuilder();
        AppendAuditQuery(sb, from, to, category, mcpAccessId, correlationId, toolName, result, skip, take);
        return sb.Length == 0 ? string.Empty : "?" + sb.ToString();
    }

    /// <summary>Appends the filter/paging query parameters (ampersand-separated) to <paramref name="sb"/>.</summary>
    private static void AppendAuditQuery(StringBuilder sb,
        DateTime? from, DateTime? to, short? category, Guid? mcpAccessId,
        Guid? correlationId, string? toolName, string? result, int? skip, int? take)
    {
        void Add(string name, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (sb.Length > 0 && sb[^1] != '?' && sb[^1] != '&') sb.Append('&');
            sb.Append(name).Append('=').Append(Uri.EscapeDataString(value));
        }
        Add("from", from?.ToString("O"));
        Add("to", to?.ToString("O"));
        Add("category", category?.ToString());
        Add("mcpAccessId", mcpAccessId?.ToString());
        Add("correlationId", correlationId?.ToString());
        Add("toolName", toolName);
        Add("result", result);
        Add("skip", skip?.ToString());
        Add("take", take?.ToString());
    }

    #endregion MCP Audit
}
