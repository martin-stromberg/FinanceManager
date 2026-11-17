using FinanceManager.Application.Mcp;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Statements;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_statement_imports_review</c> (T08): reviews committed
/// statement imports (revision-safe documents) — import headers and booked entries.
/// Scope: (Kontoauszüge, Lesen).
/// </summary>
public sealed class FmStatementImportsReviewDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_statement_imports_review";

    /// <summary>Creates the descriptor.</summary>
    public FmStatementImportsReviewDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"importId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single import; its entries are then included (entrySkip/entryTake).\"},\"accountId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional account anchor filter.\"},\"from\":{\"type\":\"string\",\"format\":\"date\",\"description\":\"Inclusive start of the commit time (UTC).\"},\"to\":{\"type\":\"string\",\"format\":\"date\",\"description\":\"Inclusive end of the commit time (UTC).\"},\"skip\":{\"type\":\"integer\",\"minimum\":0},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200},\"entrySkip\":{\"type\":\"integer\",\"minimum\":0},\"entryTake\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Reviews committed statement imports: headers (format, period, account) and booked entries with status. Scope: (Kontoauszüge, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.StatementImports, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_statement_imports_review</c>: adapts the read-only
/// <see cref="IStatementImportQueryService"/> — statement imports are revision-safe
/// documents without any write path; ownership derives via the anchor account inside the
/// service. File names and imported booking texts are untrusted content
/// (<c>untrusted_data</c>). The Kontotypen filter dimension applies via the anchor
/// account's type (denormalized into the DTO by the service).
/// </summary>
public sealed class FmStatementImportsReviewHandler : IMcpToolHandler
{
    private readonly IStatementImportQueryService _imports;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmStatementImportsReviewHandler(IStatementImportQueryService imports, McpRecordScopeFilter recordFilter)
    {
        _imports = imports;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmStatementImportsReviewDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var importId = McpToolArguments.OptionalGuid(arguments, "importId");
        var accountId = McpToolArguments.OptionalGuid(arguments, "accountId");
        var from = McpToolArguments.OptionalDateTime(arguments, "from");
        var to = McpToolArguments.OptionalDateTime(arguments, "to");
        var skip = McpToolArguments.Skip(arguments);
        var take = McpToolArguments.Take(arguments);
        var entrySkip = McpToolArguments.OptionalInt(arguments, "entrySkip") is { } es ? Math.Max(0, es) : 0;
        var entryTake = Math.Clamp(McpToolArguments.OptionalInt(arguments, "entryTake") ?? 200, 1, McpToolArguments.MaxTake);

        var imports = new List<StatementImportListItemDto>();
        if (importId.HasValue)
        {
            var single = await _imports.GetImportAsync(owner, importId.Value, ct);
            if (single is not null)
            {
                imports.Add(single);
            }
        }
        else
        {
            imports.AddRange(await _imports.ListImportsAsync(owner, accountId, from, to, skip, take, ct));
        }

        // Record-level filter (Kontotypen dimension via the anchor account's type).
        var visible = await _recordFilter.ApplyAsync(
            context.Access, McpDataType.StatementImports, imports,
            i => new McpRecordContext { AccountTypeValue = (short)i.AccountType }, ct);

        var result = new List<object>(visible.Count);
        foreach (var i in visible)
        {
            object? entries = null;
            int? entryTotal = null;
            if (importId.HasValue)
            {
                var rows = await _imports.ListEntriesAsync(owner, i.Id, entrySkip, entryTake, ct);
                entryTotal = await _imports.CountEntriesAsync(owner, i.Id, ct);
                entries = rows.Select(e => new
                {
                    id = e.Id,
                    bookingDate = e.BookingDate,
                    valutaDate = e.ValutaDate,
                    amount = e.Amount,
                    currencyCode = e.CurrencyCode,
                    isAnnounced = e.IsAnnounced,
                    isCostNeutral = e.IsCostNeutral,
                    contactId = e.ContactId,
                    savingsPlanId = e.SavingsPlanId,
                    securityTransactionId = e.SecurityTransactionId,
                    status = e.Status.ToString(),
                    untrusted_data = new
                    {
                        subject = e.Subject,
                        recipientName = e.RecipientName,
                        bookingDescription = e.BookingDescription
                    }
                }).ToList();
            }
            result.Add(new
            {
                id = i.Id,
                accountId = i.AccountId,
                accountType = i.AccountType.ToString(),
                format = i.Format.ToString(),
                importedAtUtc = i.ImportedAtUtc,
                totalEntries = i.TotalEntries,
                entryTotal,
                entries,
                untrusted_data = new { originalFileName = i.OriginalFileName }
            });
        }

        int? total = null;
        if (!importId.HasValue)
        {
            total = await _imports.CountImportsAsync(owner, accountId, ct);
        }

        return new { total, imports = result };
    }
}
