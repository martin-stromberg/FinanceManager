using FinanceManager.Application.Accounts;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Postings;
using System.Text.Json;
using IPostingsQueryService = FinanceManager.Application.Postings.IPostingsQueryService;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_postings_query</c> (T02): posting query per context
/// (account, contact, savings plan or security) with period and full-text filter.
/// Scope: (Buchungen, Lesen).
/// </summary>
public sealed class FmPostingsQueryDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_postings_query";

    /// <summary>Creates the descriptor.</summary>
    public FmPostingsQueryDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"accountId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Bank-posting context (exactly one context id required).\"},\"contactId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Contact-posting context.\"},\"savingsPlanId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Savings-plan-posting context.\"},\"securityId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Security-posting context.\"},\"q\":{\"type\":\"string\",\"description\":\"Optional search text (not supported in the securityId context).\"},\"from\":{\"type\":\"string\",\"format\":\"date\",\"description\":\"Inclusive start date (UTC).\"},\"to\":{\"type\":\"string\",\"format\":\"date\",\"description\":\"Inclusive end date (UTC).\"},\"skip\":{\"type\":\"integer\",\"minimum\":0},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Queries postings of one context (account | contact | savings plan | security) with period and text filters. Scope: (Buchungen, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Postings, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_postings_query</c>: adapts the context queries of
/// <see cref="IPostingsQueryService"/>. Exactly one context id is required; anchors not
/// owned by the access owner yield an empty list (404 semantics). Posting text fields are
/// untrusted content and are emitted inside the marked <c>untrusted_data</c> wrapper.
/// Record-level filters act through the anchor aggregate (berechtigungsmodell.md §6):
/// a bank posting carries the account type, a security posting the security category etc.
/// </summary>
public sealed class FmPostingsQueryHandler : IMcpToolHandler
{
    private readonly IPostingsQueryService _postings;
    private readonly IAccountService _accounts;
    private readonly IContactService _contacts;
    private readonly ISavingsPlanService _savingsPlans;
    private readonly ISecurityService _securities;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    /// <param name="postings">Query service for context postings.</param>
    /// <param name="accounts">Account service used for record-scope resolution.</param>
    /// <param name="contacts">Contact service used for record-scope resolution.</param>
    /// <param name="savingsPlans">Savings plan service used for record-scope resolution.</param>
    /// <param name="securities">Security service used for record-scope resolution.</param>
    /// <param name="recordFilter">Record-level scope filter applied to results.</param>
    public FmPostingsQueryHandler(
        IPostingsQueryService postings,
        IAccountService accounts,
        IContactService contacts,
        ISavingsPlanService savingsPlans,
        ISecurityService securities,
        McpRecordScopeFilter recordFilter)
    {
        _postings = postings;
        _accounts = accounts;
        _contacts = contacts;
        _savingsPlans = savingsPlans;
        _securities = securities;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmPostingsQueryDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var accountId = McpToolArguments.OptionalGuid(arguments, "accountId");
        var contactId = McpToolArguments.OptionalGuid(arguments, "contactId");
        var savingsPlanId = McpToolArguments.OptionalGuid(arguments, "savingsPlanId");
        var securityId = McpToolArguments.OptionalGuid(arguments, "securityId");
        var contextCount = new[] { accountId.HasValue, contactId.HasValue, savingsPlanId.HasValue, securityId.HasValue }.Count(b => b);
        if (contextCount != 1)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Exactly one context id is required: accountId, contactId, savingsPlanId or securityId.");
        }

        var q = McpToolArguments.OptionalString(arguments, "q");
        // The security-posting query contract has no text filter — reject instead of
        // silently dropping the argument (clear input/output behaviour).
        if (securityId.HasValue && !string.IsNullOrWhiteSpace(q))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'q' is not supported in the securityId context.");
        }
        var from = McpToolArguments.OptionalDateTime(arguments, "from");
        var to = McpToolArguments.OptionalDateTime(arguments, "to");
        var skip = McpToolArguments.Skip(arguments);
        var take = McpToolArguments.Take(arguments);

        IReadOnlyList<PostingServiceDto> postings;
        if (accountId.HasValue)
        {
            postings = await _postings.GetAccountPostingsAsync(accountId.Value, skip, take, q, from, to, owner, ct);
        }
        else if (contactId.HasValue)
        {
            postings = await _postings.GetContactPostingsAsync(contactId.Value, skip, take, q, from, to, owner, ct);
        }
        else if (savingsPlanId.HasValue)
        {
            postings = await _postings.GetSavingsPlanPostingsAsync(savingsPlanId.Value, skip, take, q, from, to, owner, ct);
        }
        else
        {
            postings = await _postings.GetSecurityPostingsAsync(securityId!.Value, skip, take, from, to, owner, ct);
        }

        var filtered = await ApplyRecordFilterAsync(context, postings, owner, ct);

        var result = filtered.Select(p => (object)new
        {
            id = p.Id,
            bookingDate = p.BookingDate,
            valutaDate = p.ValutaDate,
            amount = p.Amount,
            kind = p.Kind.ToString(),
            accountId = p.AccountId,
            contactId = p.ContactId,
            savingsPlanId = p.SavingsPlanId,
            securityId = p.SecurityId,
            sourceId = p.SourceId,
            securitySubType = p.SecuritySubType?.ToString(),
            quantity = p.Quantity,
            groupId = p.GroupId,
            linkedPostingId = p.LinkedPostingId,
            linkedPostingKind = p.LinkedPostingKind?.ToString(),
            linkedPostingAccountId = p.LinkedPostingAccountId,
            bankPostingAccountId = p.BankPostingAccountId,
            isReversed = p.IsReversed,
            isReversal = p.IsReversal,
            reversedByPostingId = p.ReversedByPostingId,
            reversalForPostingId = p.ReversalForPostingId,
            isPreliminary = p.IsPreliminary,
            informal = p.Informal,
            savingsPlanSubType = p.SavingsPlanSubType?.ToString(),
            // Posting texts are imported/user-entered content: they can carry prompt
            // injection and are separated into the marked untrusted_data wrapper
            // (sicherheitskonzept.md §5.2).
            untrusted_data = new
            {
                subject = p.Subject,
                recipientName = p.RecipientName,
                description = p.Description,
                linkedPostingAccountName = p.LinkedPostingAccountName,
                bankPostingAccountName = p.BankPostingAccountName
            }
        }).ToList();

        return new { postings = result };
    }

    private async Task<IReadOnlyList<PostingServiceDto>> ApplyRecordFilterAsync(
        McpToolCallContext context, IReadOnlyList<PostingServiceDto> postings, Guid owner, CancellationToken ct)
    {
        var scopes = await _recordFilter.GetReadScopesAsync(context.Access, McpDataType.Postings, ct);
        if (!McpRecordScopeFilter.RequiresRecordFilter(scopes))
        {
            return postings;
        }

        // Resolve classification values through the kind-anchor aggregate only for the
        // filter dimensions actually set (filter "through the aggregate", §6 of the
        // permission model). Results are cached per call to avoid repeated lookups.
        var needAccountType = scopes.Any(s => !string.IsNullOrWhiteSpace(s.AccountTypesCsv));
        var needContactCategory = scopes.Any(s => !string.IsNullOrWhiteSpace(s.ContactCategoryIdsCsv));
        var needSecurityCategory = scopes.Any(s => !string.IsNullOrWhiteSpace(s.SecurityCategoryIdsCsv));
        var needSavingsCategory = scopes.Any(s => !string.IsNullOrWhiteSpace(s.SavingsPlanCategoryIdsCsv));
        var accountTypes = new Dictionary<Guid, short?>();
        var contactCategories = new Dictionary<Guid, Guid?>();
        var securityCategories = new Dictionary<Guid, Guid?>();
        var savingsCategories = new Dictionary<Guid, Guid?>();

        var visible = new List<PostingServiceDto>(postings.Count);
        foreach (var p in postings)
        {
            short? accountTypeValue = null;
            Guid? contactCategoryId = null;
            Guid? securityCategoryId = null;
            Guid? savingsCategoryId = null;
            switch (p.Kind)
            {
                case PostingKind.Bank when p.AccountId.HasValue && needAccountType:
                    accountTypeValue = await ResolveAsync(accountTypes, p.AccountId.Value,
                        async id => (short?)(await _accounts.GetAsync(id, owner, ct))?.Type, ct);
                    break;
                case PostingKind.Contact when p.ContactId.HasValue && needContactCategory:
                    contactCategoryId = await ResolveAsync(contactCategories, p.ContactId.Value,
                        async id => (await _contacts.GetAsync(id, owner, ct))?.CategoryId, ct);
                    break;
                case PostingKind.SavingsPlan when p.SavingsPlanId.HasValue && needSavingsCategory:
                    savingsCategoryId = await ResolveAsync(savingsCategories, p.SavingsPlanId.Value,
                        async id => (await _savingsPlans.GetAsync(id, owner, ct))?.CategoryId, ct);
                    break;
                case PostingKind.Security when p.SecurityId.HasValue && needSecurityCategory:
                    securityCategoryId = await ResolveAsync(securityCategories, p.SecurityId.Value,
                        async id => (await _securities.GetAsync(id, owner, ct))?.CategoryId, ct);
                    break;
            }
            var record = new McpRecordContext
            {
                AccountTypeValue = accountTypeValue,
                ContactCategoryId = contactCategoryId,
                SecurityCategoryId = securityCategoryId,
                SavingsPlanCategoryId = savingsCategoryId
            };
            if (McpRecordScopeFilter.IsAllowed(scopes, record))
            {
                visible.Add(p);
            }
        }
        return visible;
    }

    private static async Task<T?> ResolveAsync<T>(Dictionary<Guid, T?> cache, Guid key, Func<Guid, Task<T?>> resolver, CancellationToken ct)
    {
        if (!cache.TryGetValue(key, out var value))
        {
            value = await resolver(key);
            cache[key] = value;
        }
        return value;
    }
}
