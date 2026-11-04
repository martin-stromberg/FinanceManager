namespace FinanceManager.Domain.Mcp;

/// <summary>
/// Classified data type of the MCP scope matrix (F3). Each scope tuple grants a
/// <see cref="McpRightKind"/> on exactly one of these data types.
/// </summary>
/// <remarks>
/// Persisted via <c>HasConversion&lt;short&gt;()</c>. The 18 values and their order are normative
/// (datenmodell-erweiterungen.md §2.2, berechtigungsmodell.md) and must not be changed without
/// updating the concept.
/// </remarks>
public enum McpDataType
{
    /// <summary>Bank accounts (incl. linked sub-IBANs).</summary>
    Accounts = 0,

    /// <summary>Account types.</summary>
    AccountTypes = 1,

    /// <summary>Committed statement imports and their entries.</summary>
    StatementImports = 2,

    /// <summary>Statement import drafts and their entries.</summary>
    StatementDrafts = 3,

    /// <summary>Postings (ledger entries of all kinds).</summary>
    Postings = 4,

    /// <summary>Contacts incl. alias names.</summary>
    Contacts = 5,

    /// <summary>Contact categories (Kontaktgruppen).</summary>
    ContactCategories = 6,

    /// <summary>Savings plans.</summary>
    SavingsPlans = 7,

    /// <summary>Savings plan categories.</summary>
    SavingsPlanCategories = 8,

    /// <summary>Securities incl. price history.</summary>
    Securities = 9,

    /// <summary>Security categories.</summary>
    SecurityCategories = 10,

    /// <summary>Budget purposes and budget report data.</summary>
    Budgets = 11,

    /// <summary>Budget categories.</summary>
    BudgetCategories = 12,

    /// <summary>Budget rules.</summary>
    BudgetRules = 13,

    /// <summary>Report favorites and report outputs.</summary>
    Reports = 14,

    /// <summary>KPI configurations (home/portfolio).</summary>
    KpiConfigurations = 15,

    /// <summary>Attachments (metadata and content).</summary>
    Attachments = 16,

    /// <summary>Attachment categories.</summary>
    AttachmentCategories = 17
}
