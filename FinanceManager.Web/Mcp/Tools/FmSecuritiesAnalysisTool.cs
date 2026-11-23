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
        "{\"type\":\"object\",\"properties\":{\"securityId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single security; prices are then included by default.\"},\"onlyActive\":{\"type\":\"boolean\",\"description\":\"True = only active securities (service convention); omitted = all.\"},\"includePrices\":{\"type\":\"boolean\",\"description\":\"Include historical close prices (default: only single lookups).\"},\"priceTake\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200,\"description\":\"Max price rows (clamped to 200).\"},\"includeCategories\":{\"type\":\"boolean\",\"description\":\"Include the security category list.\"},\"includeStatistics\":{\"type\":\"boolean\",\"description\":\"Include portfolio statistics over the filtered set (not affected by skip/take).\"},\"skip\":{\"type\":\"integer\",\"minimum\":0},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Security stock, categories and historical prices (read-only). Scopes: (Wertpapiere, Lesen), (Wertpapierkategorien, Lesen)."
        + " Data notes: hasPriceError marks a recorded provider price-fetch failure for the symbol; the flag is only cleared by a successful fetch, so prices may still show the last good data (see untrusted_data.priceErrorMessage for the last known provider error and priceErrorSinceUtc for its timestamp)."
        + " An empty prices array is not an error: the security may have no AlphaVantageCode configured, the provider may have returned no data, or no price exists in the requested range."
        + " A negative openQuantity means sold quantity exceeds bought quantity — a booking inconsistency (missing/duplicate postings or unmapped corporate actions), not a supported short position."
        + " includeStatistics adds a statistics object over the same filtered set as the list (securityId, onlyActive, record-scope apply; skip/take do NOT): securityCount, positionCount (openQuantity != 0), positionsWithoutPrice, byCurrency and byCategory market values (openQuantity x latest stored close), and totalMarketValue/totalCurrencyCode only when all valued positions share one currency — there is no misleading cross-currency total.";

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
    /// <param name="securities">Service used to list and load the owner's securities.</param>
    /// <param name="prices">Service used to read stored security prices (latest and history).</param>
    /// <param name="categories">Service used to resolve security category names.</param>
    /// <param name="recordFilter">Record-level scope filter applied before any price lookup.</param>
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
        var includeStatistics = McpToolArguments.OptionalBool(arguments, "includeStatistics") ?? false;
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
            // ISecurityService.ListAsync(false) is the unfiltered "all" call — a merged
            // active+all read would duplicate actives; onlyActive:false filters to
            // inactive entries in memory (the service has no inactive-only query).
            if (onlyActive is null or false)
            {
                var all = await _securities.ListAsync(owner, false, ct);
                securities.AddRange(onlyActive is null ? all : all.Where(s => !s.IsActive));
            }
            else
            {
                securities.AddRange(await _securities.ListAsync(owner, true, ct));
            }
            includePrices ??= false;
        }

        // Record-level filter (Wertpapierkategorien dimension via Security.CategoryId)
        // runs on the full filtered set BEFORE pagination so the page and the statistics
        // describe the same set and pages fill up correctly.
        var visible = await _recordFilter.ApplyAsync(
            context.Access, McpDataType.Securities, securities,
            s => new McpRecordContext { SecurityCategoryId = s.CategoryId }, ct);
        var paged = securityId.HasValue ? visible : visible.Skip(skip).Take(take).ToList();

        // Single batch lookup of the last stored close price — over the full visible set
        // when statistics are requested, otherwise over the displayed page only.
        var latestPrices = await _prices.GetLatestPricesAsync(
            owner, (includeStatistics ? visible : paged).Select(s => s.Id).ToList(), ct);

        var result = new List<object>(paged.Count);
        foreach (var s in paged)
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
                priceErrorSinceUtc = s.PriceErrorSinceUtc,
                openQuantity = s.OpenQuantity,
                latestPrice = latestPrices.TryGetValue(s.Id, out var latest)
                    ? new { close = latest.Close, date = latest.Date }
                    : null,
                prices,
                untrusted_data = new
                {
                    name = s.Name,
                    identifier = s.Identifier,
                    description = s.Description,
                    alphaVantageCode = s.AlphaVantageCode,
                    categoryName = s.CategoryName,
                    region = s.Region,
                    sector = s.Sector,
                    priceErrorMessage = s.PriceErrorMessage
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

        object? statistics = includeStatistics ? BuildStatistics(visible, latestPrices) : null;

        return new { securities = result, categories, statistics };
    }

    /// <summary>
    /// Builds the <c>statistics</c> contract over the full visible (record-scope-filtered)
    /// set — positions are securities with <c>openQuantity != 0</c>; market values use the
    /// latest stored close per position and stay currency-pure per group and total.
    /// </summary>
    /// <param name="visible">The record-scope-filtered securities before pagination.</param>
    /// <param name="latestPrices">Latest stored close price per security id.</param>
    /// <returns>The anonymous statistics contract for serialization.</returns>
    private static object BuildStatistics(IReadOnlyList<SecurityDto> visible, IReadOnlyDictionary<Guid, SecurityPriceDto> latestPrices)
    {
        var positions = visible.Where(s => (s.OpenQuantity ?? 0m) != 0m).ToList();
        var withoutPrice = positions.Where(s => !latestPrices.ContainsKey(s.Id)).ToList();
        var valued = positions.Where(s => latestPrices.ContainsKey(s.Id)).ToList();

        var byCurrency = positions
            .GroupBy(s => s.CurrencyCode)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                currencyCode = g.Key,
                positionCount = g.Count(),
                positionsWithoutPrice = g.Count(s => !latestPrices.ContainsKey(s.Id)),
                marketValue = g.Where(s => latestPrices.ContainsKey(s.Id)).Sum(s => (s.OpenQuantity ?? 0m) * latestPrices[s.Id].Close)
            }).ToList();

        var byCategory = positions
            .GroupBy(s => s.CategoryId)
            .OrderBy(g => g.Key?.ToString() ?? string.Empty)
            .Select(g =>
            {
                var valuedCurrencies = g.Where(s => latestPrices.ContainsKey(s.Id)).Select(s => s.CurrencyCode).Distinct().ToList();
                var singleCurrency = valuedCurrencies.Count == 1;
                return new
                {
                    categoryId = g.Key,
                    positionCount = g.Count(),
                    positionsWithoutPrice = g.Count(s => !latestPrices.ContainsKey(s.Id)),
                    marketValue = singleCurrency ? (decimal?)g.Where(s => latestPrices.ContainsKey(s.Id)).Sum(s => (s.OpenQuantity ?? 0m) * latestPrices[s.Id].Close) : null,
                    currencyCode = singleCurrency ? valuedCurrencies[0] : null,
                    untrusted_data = new { name = g.Select(s => s.CategoryName).FirstOrDefault(n => n is not null) }
                };
            }).ToList();

        var totalCurrencies = valued.Select(s => s.CurrencyCode).Distinct().ToList();
        var singleTotalCurrency = totalCurrencies.Count == 1;
        var priceDates = valued.Select(s => latestPrices[s.Id].Date).ToList();

        return new
        {
            securityCount = visible.Count,
            positionCount = positions.Count,
            positionsWithoutPrice = new { count = withoutPrice.Count, securityIds = withoutPrice.Select(s => s.Id).ToList() },
            byCurrency,
            byCategory,
            totalMarketValue = singleTotalCurrency ? (decimal?)valued.Sum(s => (s.OpenQuantity ?? 0m) * latestPrices[s.Id].Close) : null,
            totalCurrencyCode = singleTotalCurrency ? totalCurrencies[0] : null,
            oldestPriceDate = priceDates.Count > 0 ? (DateTime?)priceDates.Min() : null,
            newestPriceDate = priceDates.Count > 0 ? (DateTime?)priceDates.Max() : null
        };
    }
}
