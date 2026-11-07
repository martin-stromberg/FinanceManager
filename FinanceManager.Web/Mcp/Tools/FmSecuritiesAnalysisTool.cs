using FinanceManager.Application.Mcp;
using FinanceManager.Application.Securities;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Securities;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_securities_analysis</c> (T07): security stock with
/// categories and historical prices (<c>SecurityPrice</c> is exposed read-only — it has no
/// write path via MCP). Scopes: (Wertpapiere, Lesen), (Wertpapierkategorien, Lesen).
/// </summary>
public sealed class FmSecuritiesAnalysisDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_securities_analysis";

    /// <summary>Creates the descriptor.</summary>
    public FmSecuritiesAnalysisDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"securityId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single security; prices are then included by default.\"},\"onlyActive\":{\"type\":\"boolean\",\"description\":\"True = only active securities (service convention); omitted = all.\"},\"includePrices\":{\"type\":\"boolean\",\"description\":\"Include historical close prices (default: only single lookups).\"},\"priceTake\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200,\"description\":\"Max price rows (clamped to 200).\"},\"includeCategories\":{\"type\":\"boolean\",\"description\":\"Include the security category list.\"},\"skip\":{\"type\":\"integer\",\"minimum\":0},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Security stock, categories and historical prices (read-only). Scopes: (Wertpapiere, Lesen), (Wertpapierkategorien, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Securities, McpRightKind.Read),
        new McpToolScope(McpDataType.SecurityCategories, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_securities_analysis</c>: adapts <see cref="ISecurityService"/>,
/// <see cref="ISecurityPriceService"/> (prices are read-only for MCP) and
/// <see cref="ISecurityCategoryService"/>. Names, identifiers, descriptions and category
/// names are untrusted content (<c>untrusted_data</c>). The Wertpapierkategorien filter
/// dimension applies per security via its category id.
/// </summary>
public sealed class FmSecuritiesAnalysisHandler : IMcpToolHandler
{
    private readonly ISecurityService _securities;
    private readonly ISecurityPriceService _prices;
    private readonly ISecurityCategoryService _categories;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmSecuritiesAnalysisHandler(
        ISecurityService securities,
        ISecurityPriceService prices,
        ISecurityCategoryService categories,
        McpRecordScopeFilter recordFilter)
    {
        _securities = securities;
        _prices = prices;
        _categories = categories;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmSecuritiesAnalysisDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var securityId = McpToolArguments.OptionalGuid(arguments, "securityId");
        var onlyActive = McpToolArguments.OptionalBool(arguments, "onlyActive");
        var includePrices = McpToolArguments.OptionalBool(arguments, "includePrices");
        var priceTake = Math.Clamp(McpToolArguments.OptionalInt(arguments, "priceTake") ?? 60, 1, McpToolArguments.MaxTake);
        var includeCategories = McpToolArguments.OptionalBool(arguments, "includeCategories") ?? false;
        var skip = McpToolArguments.Skip(arguments);
        var take = McpToolArguments.Take(arguments);

        var securities = new List<SecurityDto>();
        if (securityId.HasValue)
        {
            var security = await _securities.GetAsync(securityId.Value, owner, ct);
            if (security is not null)
            {
                securities.Add(security);
            }
            includePrices ??= true;
        }
        else
        {
            // ISecurityService lists per active-flag; merge both partitions when no flag
            // was supplied, then apply the paged window.
            if (onlyActive is null or true)
            {
                securities.AddRange(await _securities.ListAsync(owner, true, ct));
            }
            if (onlyActive is null or false)
            {
                securities.AddRange(await _securities.ListAsync(owner, false, ct));
            }
            securities = securities.Skip(skip).Take(take).ToList();
            includePrices ??= false;
        }

        // Record-level filter (Wertpapierkategorien dimension via Security.CategoryId).
        var visible = await _recordFilter.ApplyAsync(
            context.Access, McpDataType.Securities, securities,
            s => new McpRecordContext { SecurityCategoryId = s.CategoryId }, ct);

        var result = new List<object>(visible.Count);
        foreach (var s in visible)
        {
            object? prices = null;
            if (includePrices.Value)
            {
                var rows = await _prices.ListAsync(owner, s.Id, 0, priceTake, ct);
                prices = rows.Select(p => new { date = p.Date, close = p.Close }).ToList();
            }
            result.Add(new
            {
                id = s.Id,
                categoryId = s.CategoryId,
                currencyCode = s.CurrencyCode,
                isActive = s.IsActive,
                createdUtc = s.CreatedUtc,
                archivedUtc = s.ArchivedUtc,
                symbolAttachmentId = s.SymbolAttachmentId,
                hasPriceError = s.HasPriceError,
                openQuantity = s.OpenQuantity,
                prices,
                untrusted_data = new
                {
                    name = s.Name,
                    identifier = s.Identifier,
                    description = s.Description,
                    alphaVantageCode = s.AlphaVantageCode,
                    categoryName = s.CategoryName,
                    region = s.Region,
                    sector = s.Sector
                }
            });
        }

        object? categories = null;
        if (includeCategories)
        {
            categories = (await _categories.ListAsync(owner, ct))
                .Select(c => new
                {
                    id = c.Id,
                    symbolAttachmentId = c.SymbolAttachmentId,
                    untrusted_data = new { name = c.Name }
                }).ToList();
        }

        return new { securities = result, categories };
    }
}
