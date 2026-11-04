using System.Text.Json;

namespace FinanceManager.Web.ViewModels.Mcp;

/// <summary>
/// Maps entity references found in MCP audit details (payload arguments, field-delta
/// entity types) to existing card routes (ui-konzept.md §4.5: „Ergebnis-Referenzen … →
/// Postings-Listenroute"). Only references whose target route exists today produce a
/// link — everything else stays plain text. Notably <c>AiActionRequest</c>/
/// <c>RelatedRequestId</c> and <c>importId</c> are intentionally unmapped: the approval
/// center (EP-09) and a statement-import card route do not exist yet.
/// </summary>
public static class McpAuditLinks
{
    /// <summary>One resolved entity reference (payload field name, id, target href).</summary>
    /// <param name="Field">Name of the payload field the id was taken from (e.g. <c>draftId</c>).</param>
    /// <param name="Id">Referenced entity id.</param>
    /// <param name="Href">Relative card URL (<c>/card/{kind}/{id}</c>).</param>
    public sealed record EntityRef(string Field, Guid Id, string Href);

    /// <summary>Upper bound of extracted references — protects the UI against huge payloads.</summary>
    private const int MaxReferences = 20;

    /// <summary>
    /// Payload argument names (tool-call JSON) → card route kind. Ids whose target route
    /// does not exist (e.g. <c>importId</c> — no statement-import card, <c>requestId</c> —
    /// approval center arrives with EP-09) are deliberately absent and render as text.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> _payloadFieldRoutes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["draftId"] = "statement-drafts",
            ["splitDraftId"] = "statement-drafts",
            ["entryId"] = "statement-drafts/entries",
            ["accountId"] = "accounts",
            ["contactId"] = "contacts",
            ["postingId"] = "postings",
            ["savingsPlanId"] = "savings-plans",
            ["securityId"] = "securities",
            ["accessId"] = "mcp-accesses",
            ["mcpAccessId"] = "mcp-accesses",
        };

    /// <summary>
    /// Field-delta <c>EntityType</c> names → card route kind. Unknown types (e.g.
    /// <c>McpAccessScope</c>, <c>StatementImport</c>, <c>AiActionRequest</c>) map to no
    /// route and stay plain text.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> _entityTypeRoutes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Posting"] = "postings",
            ["Account"] = "accounts",
            ["BankAccount"] = "accounts",
            ["Contact"] = "contacts",
            ["ContactCategory"] = "contacts/categories",
            ["ContactGroup"] = "contacts/categories",
            ["StatementDraft"] = "statement-drafts",
            ["StatementDraftEntry"] = "statement-drafts/entries",
            ["Security"] = "securities",
            ["SecurityCategory"] = "securities/categories",
            ["SavingsPlan"] = "savings-plans",
            ["SavingsPlanCategory"] = "savings-plans/categories",
            ["BudgetPurpose"] = "budget/purposes",
            ["BudgetRule"] = "budget/rules",
            ["BudgetCategory"] = "budget/categories",
            ["McpAccess"] = "mcp-accesses",
        };

    /// <summary>
    /// Resolves the card route of a field-delta entity (<see cref="Shared.Dtos.Mcp.McpAuditChangeDto"/>).
    /// </summary>
    /// <param name="entityType">Entity type name stored with the change.</param>
    /// <param name="entityId">Referenced entity id.</param>
    /// <returns>The relative card URL, or <see langword="null"/> when no route exists.</returns>
    public static string? TryGetEntityHref(string? entityType, Guid entityId)
        => entityType is not null && _entityTypeRoutes.TryGetValue(entityType, out var kind)
            ? $"/card/{kind}/{entityId}"
            : null;

    /// <summary>
    /// Resolves the card route of a named payload reference field (e.g. <c>draftId</c>).
    /// </summary>
    /// <param name="field">Payload field name.</param>
    /// <param name="id">Referenced entity id.</param>
    /// <returns>The relative card URL, or <see langword="null"/> when no route exists.</returns>
    public static string? TryGetPayloadFieldHref(string? field, Guid id)
        => field is not null && _payloadFieldRoutes.TryGetValue(field, out var kind)
            ? $"/card/{kind}/{id}"
            : null;

    /// <summary>
    /// Extracts linkable entity references from a raw payload JSON snapshot: every known
    /// <c>*Id</c> argument field whose value parses as a <see cref="Guid"/> and whose
    /// target route exists becomes an <see cref="EntityRef"/>. Unknown fields, non-Guid
    /// values and malformed JSON never throw — they simply yield no reference.
    /// </summary>
    /// <param name="payloadJson">Raw payload JSON (graduated tool-call arguments).</param>
    /// <returns>Resolved references (deduplicated, capped at <see cref="MaxReferences"/>).</returns>
    public static IReadOnlyList<EntityRef> ExtractPayloadReferences(string? payloadJson)
    {
        var result = new List<EntityRef>();
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return result;
        }
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Collect(doc.RootElement, result, seen);
        }
        catch (JsonException)
        {
            // malformed/truncated snapshot → no references, never an exception
        }
        return result;
    }

    private static void Collect(JsonElement el, List<EntityRef> result, HashSet<string> seen)
    {
        if (result.Count >= MaxReferences)
        {
            return;
        }
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    if (result.Count >= MaxReferences)
                    {
                        return;
                    }
                    if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        Collect(prop.Value, result, seen);
                        continue;
                    }
                    if (prop.Value.ValueKind == JsonValueKind.String
                        && Guid.TryParse(prop.Value.GetString(), out var id)
                        && id != Guid.Empty
                        && TryGetPayloadFieldHref(prop.Name, id) is { } href
                        && seen.Add(prop.Name + "|" + id))
                    {
                        result.Add(new EntityRef(prop.Name, id, href));
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                {
                    Collect(item, result, seen);
                }
                break;
        }
    }
}
