using FinanceManager.Domain.Mcp;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// The approval profile of one tool action: which (Datentyp × Rechteart) grant covers the
/// operation and whether the operation is a routine write-preparing step or carries an
/// approval-required effect (approval-konzept.md §3.5, granular mode).
/// </summary>
/// <param name="DataType">Data type the matching <see cref="McpAccessApprovalGrant"/> must carry.</param>
/// <param name="Right">Right kind the matching grant must carry.</param>
/// <param name="RequiresApproval">
/// <see langword="true"/> for operations whose effect stays approval-bound in the
/// granular mode (final/destructive effects — e.g. admin mutations, deletes, merges,
/// archives). A matching grant lets such a request enter the queue directly
/// <see cref="AiActionRequestStatus.Approved"/> (<see cref="PreApprovalKind.GrantPreApproved"/>)
/// instead of pending; the request record stays the documented approval instance.
/// <see langword="false"/> for routine operations a matching grant releases directly
/// without a request at all.
/// </param>
/// <returns>The approval profile of one tool action.</returns>
public sealed record McpApprovalActionProfile(
    McpDataType DataType,
    McpRightKind Right,
    bool RequiresApproval);

/// <summary>
/// Static operation → approval-profile catalog keyed by (tool name, action key). The
/// action key is the <c>operation</c> argument of the tool call, or the tool name itself
/// for single-action tools (the same key <see cref="AiActionRequest.ActionKey"/> stores).
/// </summary>
/// <remarks>
/// <para>
/// Failing closed is deliberate: a tool/action with no catalog entry is treated as
/// approval-bound — a grant can never silently pre-approve an unmapped operation.
/// Tools whose operation→(data type, right) mapping is ambiguous are intentionally not
/// listed: <c>fm_categories_manage</c>, <c>fm_budgets_manage</c> and
/// <c>fm_reports_configuration_manage</c> each span several data types selected by an
/// argument rather than the operation, so they stay approval-bound under
/// <see cref="McpApprovalMode.Granular"/> until the catalog can express the distinction.
/// </para>
/// <para>
/// The catalog only interprets write-class tools — read/suggest/analyze tools run
/// directly for every approval mode and never consult it.
/// </para>
/// </remarks>
public static class McpApprovalActionCatalog
{
    private static readonly Dictionary<string, Dictionary<string, McpApprovalActionProfile>> Profiles = new()
    {
        // ---------------------------------------------------------------
        // Administrative surfaces (fm_admin_*): read/list/status/download
        // are routine (Administration, Read); every mutating operation is
        // approval-required on Modify or Create (the mirrors of the
        // in-handler RequireRightAsync checks).
        // ---------------------------------------------------------------
        ["fm_admin_updates"] = new()
        {
            ["status"] = new(McpDataType.Administration, McpRightKind.Read, false),
            ["check"] = new(McpDataType.Administration, McpRightKind.Read, false),
            ["install"] = new(McpDataType.Administration, McpRightKind.Modify, true),
            ["schedule"] = new(McpDataType.Administration, McpRightKind.Modify, true),
        },
        ["fm_admin_backups"] = new()
        {
            ["list"] = new(McpDataType.Administration, McpRightKind.Read, false),
            ["download"] = new(McpDataType.Administration, McpRightKind.Read, false),
            ["create"] = new(McpDataType.Administration, McpRightKind.Create, true),
            ["upload"] = new(McpDataType.Administration, McpRightKind.Create, true),
            ["delete"] = new(McpDataType.Administration, McpRightKind.Modify, true),
            ["apply"] = new(McpDataType.Administration, McpRightKind.Modify, true),
        },
        ["fm_admin_notifications"] = new()
        {
            ["list"] = new(McpDataType.Administration, McpRightKind.Read, false),
            ["dismiss"] = new(McpDataType.Administration, McpRightKind.Modify, true),
        },
        ["fm_admin_settings"] = new()
        {
            ["get"] = new(McpDataType.Administration, McpRightKind.Read, false),
            ["set"] = new(McpDataType.Administration, McpRightKind.Modify, true),
        },
        ["fm_admin_status"] = new()
        {
            ["fm_admin_status"] = new(McpDataType.Administration, McpRightKind.Read, false),
        },

        // ---------------------------------------------------------------
        // Master-data manage tools: create/update/alias/link/set ops are
        // routine write-preparing operations on Create or Modify;
        // delete/merge/archive are approval-required on Delete (a right
        // kind grants can never carry — they stay approval-bound always).
        // ---------------------------------------------------------------
        ["fm_accounts_manage"] = new()
        {
            ["create"] = new(McpDataType.Accounts, McpRightKind.Create, false),
            ["update"] = new(McpDataType.Accounts, McpRightKind.Modify, false),
            ["addLinkedIban"] = new(McpDataType.Accounts, McpRightKind.Modify, false),
            ["removeLinkedIban"] = new(McpDataType.Accounts, McpRightKind.Modify, false),
            ["delete"] = new(McpDataType.Accounts, McpRightKind.Delete, true),
        },
        ["fm_attachments_manage"] = new()
        {
            ["upload"] = new(McpDataType.Attachments, McpRightKind.Create, false),
            ["createUrl"] = new(McpDataType.Attachments, McpRightKind.Create, false),
            ["createReference"] = new(McpDataType.Attachments, McpRightKind.Create, false),
            ["update"] = new(McpDataType.Attachments, McpRightKind.Modify, false),
            ["setCategory"] = new(McpDataType.Attachments, McpRightKind.Modify, false),
            ["reassign"] = new(McpDataType.Attachments, McpRightKind.Modify, false),
            ["delete"] = new(McpDataType.Attachments, McpRightKind.Delete, true),
        },
        ["fm_contacts_manage"] = new()
        {
            ["create"] = new(McpDataType.Contacts, McpRightKind.Create, false),
            ["update"] = new(McpDataType.Contacts, McpRightKind.Modify, false),
            ["addAlias"] = new(McpDataType.Contacts, McpRightKind.Modify, false),
            ["removeAlias"] = new(McpDataType.Contacts, McpRightKind.Modify, false),
            ["merge"] = new(McpDataType.Contacts, McpRightKind.Delete, true),
            ["delete"] = new(McpDataType.Contacts, McpRightKind.Delete, true),
        },
        ["fm_savings_plans_manage"] = new()
        {
            ["create"] = new(McpDataType.SavingsPlans, McpRightKind.Create, false),
            ["update"] = new(McpDataType.SavingsPlans, McpRightKind.Modify, false),
            ["addAccountLink"] = new(McpDataType.SavingsPlans, McpRightKind.Modify, false),
            ["removeAccountLink"] = new(McpDataType.SavingsPlans, McpRightKind.Modify, false),
            ["reportActualBalance"] = new(McpDataType.SavingsPlans, McpRightKind.Modify, false),
            ["archive"] = new(McpDataType.SavingsPlans, McpRightKind.Delete, true),
        },
        ["fm_securities_manage"] = new()
        {
            ["create"] = new(McpDataType.Securities, McpRightKind.Create, false),
            ["update"] = new(McpDataType.Securities, McpRightKind.Modify, false),
            ["unarchive"] = new(McpDataType.Securities, McpRightKind.Modify, false),
            ["archive"] = new(McpDataType.Securities, McpRightKind.Delete, true),
        },

        // ---------------------------------------------------------------
        // Statement draft workflow: edits/import/complete are routine on
        // Modify/Create/Complete; book and discard are the final,
        // approval-required effects on Book and Delete.
        // ---------------------------------------------------------------
        ["fm_statement_draft_edit"] = new()
        {
            ["updateEntry"] = new(McpDataType.StatementDrafts, McpRightKind.Modify, false),
            ["addEntry"] = new(McpDataType.StatementDrafts, McpRightKind.Modify, false),
            ["deleteEntry"] = new(McpDataType.StatementDrafts, McpRightKind.Modify, false),
            ["resetDuplicate"] = new(McpDataType.StatementDrafts, McpRightKind.Modify, false),
            ["assignAccount"] = new(McpDataType.StatementDrafts, McpRightKind.Modify, false),
        },
        ["fm_statement_draft_import"] = new()
        {
            ["fm_statement_draft_import"] = new(McpDataType.StatementDrafts, McpRightKind.Create, false),
        },
        ["fm_statement_draft_complete"] = new()
        {
            ["fm_statement_draft_complete"] = new(McpDataType.StatementDrafts, McpRightKind.Complete, false),
        },
        ["fm_statement_draft_book"] = new()
        {
            ["fm_statement_draft_book"] = new(McpDataType.StatementDrafts, McpRightKind.Book, true),
        },
        ["fm_statement_draft_discard"] = new()
        {
            ["fm_statement_draft_discard"] = new(McpDataType.StatementDrafts, McpRightKind.Delete, true),
        },
        ["fm_posting_reverse"] = new()
        {
            ["fm_posting_reverse"] = new(McpDataType.Postings, McpRightKind.Delete, true),
        },
    };

    /// <summary>
    /// Looks up the approval profile of a (tool, action) pair.
    /// </summary>
    /// <param name="toolName">The <c>fm_*</c> tool name.</param>
    /// <param name="actionKey">The operation key; <see langword="null"/>/empty falls back
    /// to the tool name (the dispatcher's default action key for single-action tools).</param>
    /// <returns>The profile, or <see langword="null"/> when unmapped (fail closed).</returns>
    public static McpApprovalActionProfile? Find(string toolName, string? actionKey)
    {
        if (string.IsNullOrEmpty(toolName) || !Profiles.TryGetValue(toolName, out var ops))
        {
            return null;
        }
        var key = string.IsNullOrEmpty(actionKey) ? toolName : actionKey;
        return ops.TryGetValue(key, out var profile) ? profile : null;
    }
}
