using FinanceManager.Web.Services;
using Microsoft.Extensions.Localization;
using System.Globalization;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Resource contract of the approval center (ui-konzept.md §4.6/§6.2, F-1001): the inbox row
/// resolves a localized process label per queueing tool via <c>McpApproval_ToolLabel_*</c>
/// (fallback = raw <c>fm_*</c> name) and the history maps classified <c>Err_*</c> failure
/// codes onto existing resources (fallback = raw code). These tests pin the key set for
/// both shipped cultures — a missing de/en key degrades the UI to raw technical names.
/// </summary>
public sealed class McpApprovalResourceTests
{
    /// <summary>Every tool whose calls can land in the approval queue (T20/T21/T31 final
    /// tools + the T22–T29 manage tools with destructive sub-operations).</summary>
    private static readonly string[] QueueingTools =
    {
        "fm_statement_draft_book", "fm_statement_draft_discard", "fm_posting_reverse",
        "fm_contacts_manage", "fm_accounts_manage", "fm_savings_plans_manage",
        "fm_securities_manage", "fm_budgets_manage", "fm_categories_manage",
        "fm_attachments_manage", "fm_reports_configuration_manage"
    };

    /// <summary>Classified failure codes surfaced by the request execution paths
    /// (executor classifications + handler-level <c>McpToolException</c> codes).</summary>
    private static readonly string[] FailureCodes =
    {
        "Err_TargetGone", "Err_AlreadyReversed", "Err_ValidationFailed",
        "Err_RightsRevoked", "Err_ExecutionTimeout", "Err_Internal",
        "Err_Mcp_InvalidState", "Err_Mcp_InvalidArguments", "Err_Mcp_InvalidScope",
        "Err_Mcp_ScopeDenied", "Err_Mcp_UnknownTool", "Err_Mcp_ToolNotImplemented",
        "Err_Mcp_PendingLimit", "Err_NotFound"
    };

    /// <summary>The production lookup path of the panels — resolves the <c>Pages</c>
    /// resource set against <see cref="CultureInfo.CurrentUICulture"/> per call.</summary>
    private static IStringLocalizer Localizer() => new PagesStringLocalizer();

    /// <summary>Run <paramref name="check"/> under the given UI culture (per-thread, restored).</summary>
    private static void WithCulture(string culture, Action<IStringLocalizer> check)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            check(Localizer());
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>Each queueing tool has a localized inbox process label in de and en.</summary>
    [Fact]
    public void ToolLabels_Exist_ForAllQueueingTools_DeAndEn()
    {
        foreach (var culture in new[] { "de", "en" })
        {
            WithCulture(culture, l =>
            {
                foreach (var tool in QueueingTools)
                {
                    var entry = l[$"McpApproval_ToolLabel_{tool}"];
                    Assert.False(entry.ResourceNotFound, $"[{culture}] missing McpApproval_ToolLabel_{tool}");
                    Assert.False(string.IsNullOrWhiteSpace(entry.Value));
                }
            });
        }
    }

    /// <summary>Each classified failure code maps to a localized text in de and en.</summary>
    [Fact]
    public void FailureCodes_Map_ToLocalizedText_DeAndEn()
    {
        foreach (var culture in new[] { "de", "en" })
        {
            WithCulture(culture, l =>
            {
                foreach (var code in FailureCodes)
                {
                    var entry = l[code];
                    Assert.False(entry.ResourceNotFound, $"[{culture}] missing {code}");
                    Assert.NotEqual(code, entry.Value);
                }
            });
        }
    }

    /// <summary>The tool detail label used by the expanded request view exists.</summary>
    [Fact]
    public void DetailToolLabel_Exists_DeAndEn()
    {
        foreach (var culture in new[] { "de", "en" })
        {
            WithCulture(culture, l => Assert.False(l["McpApproval_Detail_Tool"].ResourceNotFound,
                $"[{culture}] missing McpApproval_Detail_Tool"));
        }
    }
}
