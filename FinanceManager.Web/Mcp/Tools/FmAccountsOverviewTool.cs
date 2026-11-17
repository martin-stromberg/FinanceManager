using FinanceManager.Application.Accounts;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Accounts;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_accounts_overview</c> (T01): account overview with
/// balances, types, IBANs, linked sub-IBANs and account statistics.
/// Scopes: (Konten, Lesen), (Kontotypen, Lesen).
/// </summary>
public sealed class FmAccountsOverviewDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_accounts_overview";

    /// <summary>Creates the descriptor.</summary>
    public FmAccountsOverviewDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"accountId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single account id; omitted = paged list.\"},\"q\":{\"type\":\"string\",\"description\":\"Optional account search text.\"},\"bankContactId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional bank contact filter.\"},\"includeStatistics\":{\"type\":\"boolean\",\"description\":\"Include account statistics.\"},\"includeLinkedIbans\":{\"type\":\"boolean\",\"description\":\"Include linked sub-IBANs of collection accounts (default true).\"},\"skip\":{\"type\":\"integer\",\"minimum\":0},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Lists the owner's bank accounts with balances, type, IBAN and linked sub-IBANs, plus optional statistics. Scopes: (Konten, Lesen), (Kontotypen, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Accounts, McpRightKind.Read),
        new McpToolScope(McpDataType.AccountTypes, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_accounts_overview</c>: adapts <see cref="IAccountService"/> list/get and
/// statistics with the owner id of the access. Foreign or filtered accounts behave like
/// non-existing ones (empty result). User-entered fields (name, IBAN, linked IBANs, bank
/// group display names) are emitted inside the marked <c>untrusted_data</c> wrapper
/// (sicherheitskonzept.md §5.2).
/// </summary>
public sealed class FmAccountsOverviewHandler : IMcpToolHandler
{
    private readonly IAccountService _accounts;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmAccountsOverviewHandler(IAccountService accounts, McpRecordScopeFilter recordFilter)
    {
        _accounts = accounts;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmAccountsOverviewDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var accountId = McpToolArguments.OptionalGuid(arguments, "accountId");
        var q = McpToolArguments.OptionalString(arguments, "q");
        var bankContactId = McpToolArguments.OptionalGuid(arguments, "bankContactId");
        var includeStatistics = McpToolArguments.OptionalBool(arguments, "includeStatistics") ?? false;
        var includeLinkedIbans = McpToolArguments.OptionalBool(arguments, "includeLinkedIbans") ?? true;
        var skip = McpToolArguments.Skip(arguments);
        var take = McpToolArguments.Take(arguments);

        var accounts = new List<AccountDto>();
        if (accountId.HasValue)
        {
            var account = await _accounts.GetAsync(accountId.Value, owner, ct);
            if (account is not null)
            {
                accounts.Add(account);
            }
        }
        else
        {
            accounts.AddRange(await _accounts.ListAsync(owner, skip, take, bankContactId, q, ct));
        }

        // Record-level filter (Kontotypen dimension) — records not matched by a set
        // whitelist are invisible (empty result, no forbidden/not-found distinction).
        var visible = await _recordFilter.ApplyAsync(
            context.Access, McpDataType.Accounts, accounts,
            a => new McpRecordContext { AccountTypeValue = (short)a.Type }, ct);

        var result = new List<object>(visible.Count);
        foreach (var a in visible)
        {
            IReadOnlyList<string> linkedIbans = a.LinkedIbans;
            if (includeLinkedIbans && linkedIbans.Count == 0 && a.IsCollectionAccount)
            {
                linkedIbans = await _accounts.GetLinkedIbansAsync(a.Id, owner, ct) ?? Array.Empty<string>();
            }
            result.Add(new
            {
                id = a.Id,
                type = a.Type.ToString(),
                currentBalance = a.CurrentBalance,
                bankContactId = a.BankContactId,
                symbolAttachmentId = a.SymbolAttachmentId,
                savingsPlanExpectation = a.SavingsPlanExpectation.ToString(),
                securityProcessingEnabled = a.SecurityProcessingEnabled,
                isCollectionAccount = a.IsCollectionAccount,
                untrusted_data = new
                {
                    name = a.Name,
                    iban = a.Iban,
                    linkedIbans = includeLinkedIbans ? linkedIbans : Array.Empty<string>()
                }
            });
        }

        object? statistics = null;
        if (includeStatistics)
        {
            var stats = await _accounts.GetStatisticsAsync(owner, q, ct);
            statistics = new
            {
                accountCount = stats.AccountCount,
                totalBalance = stats.TotalBalance,
                yearToDateChange = stats.YearToDateChange,
                monthToDateChange = stats.MonthToDateChange,
                totalGrossMagnitude = stats.TotalGrossMagnitude,
                byAccountType = stats.ByAccountType.Select(g => new
                {
                    key = g.Key,
                    netBalance = g.NetBalance,
                    positiveBalance = g.PositiveBalance,
                    negativeBalanceMagnitude = g.NegativeBalanceMagnitude,
                    grossMagnitude = g.GrossMagnitude,
                    accountCount = g.AccountCount,
                    zeroBalanceCount = g.ZeroBalanceCount,
                    untrusted_data = new { displayName = g.DisplayName }
                }).ToList(),
                byBankContact = stats.ByBankContact.Select(g => new
                {
                    key = g.Key,
                    netBalance = g.NetBalance,
                    positiveBalance = g.PositiveBalance,
                    negativeBalanceMagnitude = g.NegativeBalanceMagnitude,
                    grossMagnitude = g.GrossMagnitude,
                    accountCount = g.AccountCount,
                    zeroBalanceCount = g.ZeroBalanceCount,
                    untrusted_data = new { displayName = g.DisplayName }
                }).ToList()
            };
        }

        return new { accounts = result, statistics };
    }
}
