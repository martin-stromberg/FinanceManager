using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Mcp;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Mcp;

/// <summary>
/// Shared localization helpers for MCP audit entries used by the audit timeline and the
/// usage overview (Klartextzeilen de/en, ui-konzept.md §4.5 — keine Roh-JSON-Pflicht).
/// Keeps the mapping from event codes to resource keys in a single place.
/// </summary>
public static class McpAuditText
{
    /// <summary>Resource key prefix for category names.</summary>
    public const string CategoryKeyPrefix = "McpAudit_Category_";

    /// <summary>Resource key prefix for result codes.</summary>
    public const string ResultKeyPrefix = "McpAudit_Result_";

    /// <summary>Resource key prefix for localized event lines.</summary>
    public const string LineKeyPrefix = "McpAudit_Line_";

    /// <summary>
    /// Returns the localized category name (McpAuditCategory as short).
    /// </summary>
    /// <param name="localizer">Localizer for the <c>Pages</c> resources.</param>
    /// <param name="category">Category value from the DTO.</param>
    /// <returns>Localized category name or the raw value.</returns>
    public static string Category(IStringLocalizer localizer, short category)
    {
        var name = Enum.IsDefined(typeof(McpAuditCategory), (int)category)
            ? ((McpAuditCategory)category).ToString()
            : category.ToString();
        return Lookup(localizer, CategoryKeyPrefix + name, name);
    }

    /// <summary>
    /// Returns the localized result text of an entry (<c>Success</c>, <c>Denied</c>, …).
    /// Unknown codes fall back to the raw code text.
    /// </summary>
    /// <param name="localizer">Localizer for the <c>Pages</c> resources.</param>
    /// <param name="result">Stable result code.</param>
    /// <returns>Localized result text or the raw code.</returns>
    public static string Result(IStringLocalizer localizer, string? result)
        => string.IsNullOrWhiteSpace(result)
            ? string.Empty
            : Lookup(localizer, ResultKeyPrefix + result, result);

    /// <summary>
    /// Returns a hint color for the category badge (ToolCall neutral, DataChange accent,
    /// security events warning-tinted).
    /// </summary>
    /// <param name="category">Category value from the DTO.</param>
    /// <returns>A CSS color value.</returns>
    public static string CategoryColor(short category)
        => (McpAuditCategory)category switch
        {
            McpAuditCategory.SecurityEvent => "#e6a03c",
            McpAuditCategory.DataChange => "#7cc37c",
            McpAuditCategory.ApprovalEvent => "#9db8ff",
            _ => "var(--muted)"
        };

    /// <summary>
    /// Builds the localized one-line event text of a timeline row (audit-konzept.md §6.1:
    /// "verständliche Darstellung"). Known event types get dedicated templates; everything
    /// else falls back to the generic "<c>{eventType}</c>" template.
    /// </summary>
    /// <param name="localizer">Localizer for the <c>Pages</c> resources.</param>
    /// <param name="e">The audit list item.</param>
    /// <returns>The localized event line.</returns>
    public static string EventLine(IStringLocalizer localizer, McpAuditEntryListItemDto e)
    {
        var key = LineKeyPrefix + e.EventType;
        var entry = localizer[key];
        if (!entry.ResourceNotFound && !string.IsNullOrWhiteSpace(entry.Value))
        {
            return string.Format(entry.Value, e.ToolName ?? string.Empty,
                e.McpAccessName ?? string.Empty, e.AffectedCount ?? 0);
        }
        var generic = localizer[LineKeyPrefix + "Generic"];
        return generic.ResourceNotFound
            ? e.EventType
            : string.Format(generic.Value, e.EventType);
    }

    private static string Lookup(IStringLocalizer localizer, string key, string fallback)
    {
        var value = localizer[key];
        return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value) ? fallback : value.Value;
    }
}
