using FinanceManager.Shared.Dtos.Mcp;
using System.Text.Json;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// Server-side effect preview of an approval request (F-1001; approval-konzept.md §7.2,
/// ui-konzept.md §4.6): classifies the frozen <c>ParameterSnapshot</c> of a request into a
/// stable effect kind, flags final/irreversible effects (the approval center confirms them
/// via the critical confirmation severity) and flattens the relevant scalar parameters so
/// the UI renders a concrete "what would happen" view without interpreting raw JSON.
/// </summary>
/// <remarks>
/// The classification follows tool name + action key (the sub-operation discriminator the
/// dispatcher derives from the <c>operation</c> argument). Snapshot values are untrusted
/// content by origin — the preview exposes them as display data only.
/// </remarks>
public static class McpApprovalEffectPreview
{
    /// <summary>Maximum number of scalar snapshot fields kept in the preview.</summary>
    private const int MaxFields = 24;

    /// <summary>
    /// Snapshot properties that never reach the preview: the client reason text (shown
    /// separately as untrusted content), the idempotency key and nested objects.
    /// </summary>
    private static readonly HashSet<string> ExcludedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "reasonText", "reason", "idempotencyKey"
    };

    /// <summary>
    /// Builds the effect preview of a request from its tool name, action key and frozen
    /// parameter snapshot.
    /// </summary>
    /// <param name="toolName">The <c>fm_*</c> tool name.</param>
    /// <param name="actionKey">Sub-operation discriminator of the call.</param>
    /// <param name="parameterSnapshotJson">Frozen JSON parameter snapshot.</param>
    /// <returns>The classified preview, or <see langword="null"/> for a generic/empty snapshot.</returns>
    public static AiActionRequestEffectPreviewDto? Build(string toolName, string actionKey, string parameterSnapshotJson)
    {
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(parameterSnapshotJson);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            root = default;
        }

        var fields = new List<AiActionRequestEffectFieldDto>();
        var bookAll = false;
        var discardAll = false;
        var hasCommitParameters = false;
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (fields.Count >= MaxFields)
                {
                    break;
                }
                var value = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => prop.Value.GetRawText(),
                    JsonValueKind.Null => null,
                    _ => null // nested objects/arrays stay out of the flat preview
                };
                if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Undefined)
                {
                    continue;
                }
                if (string.Equals(prop.Name, "bookAll", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.True)
                {
                    bookAll = true;
                }
                if (string.Equals(prop.Name, "all", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.True)
                {
                    discardAll = true;
                }
                if ((string.Equals(prop.Name, "accountId", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(prop.Name, "importFormat", StringComparison.OrdinalIgnoreCase))
                    && prop.Value.ValueKind != JsonValueKind.Null)
                {
                    hasCommitParameters = true;
                }
                if (ExcludedFields.Contains(prop.Name))
                {
                    continue;
                }
                fields.Add(new AiActionRequestEffectFieldDto(prop.Name, value));
            }
        }

        var kind = Classify(toolName, actionKey, bookAll, discardAll, hasCommitParameters);
        var destructive = kind is "merge" or "delete" or "archive" or "reverse"
            or "discard" or "discardAll" or "book" or "bookAll" or "commit";
        return new AiActionRequestEffectPreviewDto(kind, destructive, fields);
    }

    /// <summary>
    /// Maps tool + action key to the stable effect kind. Final ledger effects
    /// (book/commit/reverse) are keyed by tool; the pflege sub-operations by action key.
    /// </summary>
    private static string Classify(string toolName, string actionKey, bool bookAll, bool discardAll, bool hasCommitParameters)
        => toolName switch
        {
            "fm_statement_draft_book" => bookAll ? "bookAll" : hasCommitParameters ? "commit" : "book",
            "fm_statement_draft_discard" => discardAll ? "discardAll" : "discard",
            "fm_posting_reverse" => "reverse",
            _ => actionKey switch
            {
                "merge" => "merge",
                "delete" => "delete",
                "archive" => "archive",
                _ => "generic"
            }
        };
}
