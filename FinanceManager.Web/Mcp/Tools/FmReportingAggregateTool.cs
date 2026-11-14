using FinanceManager.Application.Mcp;
using FinanceManager.Application.Reports;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Reports;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_reporting_aggregate</c> (T03): aggregated report
/// evaluations and time series over postings — either from an inline query or by executing
/// a stored report favorite. Scopes: (Buchungen, Lesen), (Reports, Lesen).
/// </summary>
public sealed class FmReportingAggregateDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_reporting_aggregate";

    private const string JsonSchema = "{\"type\":\"object\",\"properties\":{\"favoriteId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Execute a stored report favorite instead of an inline query.\"},\"postingKind\":{\"type\":\"string\",\"enum\":[\"Bank\",\"Contact\",\"SavingsPlan\",\"Security\"],\"description\":\"Required for inline queries.\"},\"interval\":{\"type\":\"string\",\"enum\":[\"Month\",\"Quarter\",\"HalfYear\",\"Year\",\"Ytd\",\"AllHistory\"],\"description\":\"Period granularity. 'Ytd' compares year-to-date slices across ALL years (one bucket per calendar year) — it is NOT limited to the current year; filter points by periodStart or pass 'year'. 'Year' returns one bucket per calendar year, 'AllHistory' a single total.\"},\"year\":{\"type\":\"integer\",\"minimum\":1900,\"maximum\":9999,\"description\":\"Optional calendar-year filter applied to the returned points' periodStart (e.g. 2026). Without it the result may span many years — the 'coverage' field states the span.\"},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200,\"description\":\"Max periods/groups (clamped to 200).\"},\"includeCategory\":{\"type\":\"boolean\"},\"comparePrevious\":{\"type\":\"boolean\"},\"compareYear\":{\"type\":\"boolean\"},\"compareProjection\":{\"type\":\"boolean\"},\"useValutaDate\":{\"type\":\"boolean\"},\"accountIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"contactIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"savingsPlanIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"securityIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"contactCategoryIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"savingsPlanCategoryIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"securityCategoryIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"securitySubTypes\":{\"type\":\"array\",\"items\":{\"type\":\"integer\"},\"description\":\"Restrict security postings to these sub type codes (0=Buy,1=Sell,2=Dividend,3=Fee,4=Tax). Security detail points are always split per sub type — see 'postingType' on each returned point.\"},\"bankTextFilters\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"maxLength\":500},\"description\":\"Free text filters for bank postings (OR linked; matches subject/description, case-insensitive substring).\"},\"contactTextFilters\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"maxLength\":500},\"description\":\"Free text filters for contact postings (OR linked; matches subject/description, case-insensitive substring).\"},\"savingsPlanTextFilters\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"maxLength\":500},\"description\":\"Free text filters for savings plan postings (OR linked; matches subject/description, case-insensitive substring).\"},\"securityTextFilters\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"maxLength\":500},\"description\":\"Free text filters for security postings (OR linked; matches subject/description, case-insensitive substring).\"}},\"additionalProperties\":false}";

    /// <summary>Creates the descriptor.</summary>
    public FmReportingAggregateDescriptor()
        : base(JsonSchema)
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Runs an aggregated posting report (sums per period/group) either inline or from a stored report favorite. Security detail points are split per postingType (Buy/Sell/Dividend/Fee/Tax): each point's amount covers only that sub type and its groupKey carries it as a suffix ('Security:{id}:{postingType}'). Roll-up rows (Category:/Type:), netted dividend/projection rows and all non-security points have postingType=null — so coverage.pointCount grows with the number of distinct sub types per security and period. Scopes: (Buchungen, Lesen), (Reports, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Postings, McpRightKind.Read),
        new McpToolScope(McpDataType.Reports, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_reporting_aggregate</c>: adapts <see cref="IReportAggregationService"/>
/// and <see cref="IReportFavoriteService"/>. The aggregation service scopes all filters to
/// the owner internally, so foreign ids in the filter lists simply match nothing (empty
/// result, no error). Group/category names derived from master data are untrusted content.
/// </summary>
public sealed class FmReportingAggregateHandler : IMcpToolHandler
{
    private readonly IReportAggregationService _aggregation;
    private readonly IReportFavoriteService _favorites;

    /// <summary>Creates the handler.</summary>
    /// <param name="aggregation">Report aggregation service used to execute queries.</param>
    /// <param name="favorites">Report favorite service used to load stored favorites.</param>
    public FmReportingAggregateHandler(IReportAggregationService aggregation, IReportFavoriteService favorites)
    {
        _aggregation = aggregation;
        _favorites = favorites;
    }

    /// <inheritdoc />
    public string ToolName => FmReportingAggregateDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var favoriteId = McpToolArguments.OptionalGuid(arguments, "favoriteId");

        ReportAggregationQuery query;
        object? favoriteOut = null;
        if (favoriteId.HasValue)
        {
            var favorite = await _favorites.GetAsync(favoriteId.Value, owner, ct);
            if (favorite is null)
            {
                // Foreign/unknown favorites behave like non-existing ones (no distinction).
                return new { favorite = (object?)null, result = (object?)null };
            }
            favoriteOut = new
            {
                id = favorite.Id,
                postingKind = favorite.PostingKind.ToString(),
                interval = favorite.Interval.ToString(),
                useValutaDate = favorite.UseValutaDate,
                savingsPlanPostingSource = favorite.SavingsPlanPostingSource.ToString(),
                untrusted_data = new { name = favorite.Name }
            };
            var f = favorite.Filters;
            query = new ReportAggregationQuery(
                owner,
                favorite.PostingKind,
                favorite.Interval,
                Math.Clamp(favorite.Take, 1, McpToolArguments.MaxTake),
                favorite.IncludeCategory,
                favorite.ComparePrevious,
                favorite.CompareYear,
                favorite.CompareProjection,
                favorite.PostingKinds,
                AnalysisDate: null,
                favorite.UseValutaDate,
                f is null ? null : new ReportAggregationFilters(
                    AccountIds: f.AccountIds,
                    ContactIds: f.ContactIds,
                    SavingsPlanIds: f.SavingsPlanIds,
                    SecurityIds: f.SecurityIds,
                    ContactCategoryIds: f.ContactCategoryIds,
                    SavingsPlanCategoryIds: f.SavingsPlanCategoryIds,
                    SecurityCategoryIds: f.SecurityCategoryIds,
                    SecuritySubTypes: f.SecuritySubTypes,
                    IncludeDividendRelated: f.IncludeDividendRelated,
                    BankTextFilters: f.BankTextFilters,
                    ContactTextFilters: f.ContactTextFilters,
                    SavingsPlanTextFilters: f.SavingsPlanTextFilters,
                    SecurityTextFilters: f.SecurityTextFilters),
                favorite.SavingsPlanPostingSource);
        }
        else
        {
            var postingKind = McpToolArguments.OptionalEnum<PostingKind>(arguments, "postingKind")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                    "Either 'favoriteId' or 'postingKind' is required.");
            var interval = McpToolArguments.OptionalEnum<ReportInterval>(arguments, "interval") ?? ReportInterval.Month;
            var take = Math.Clamp(McpToolArguments.OptionalInt(arguments, "take") ?? 12, 1, McpToolArguments.MaxTake);
            var includeCategory = McpToolArguments.OptionalBool(arguments, "includeCategory") ?? false;
            var comparePrevious = McpToolArguments.OptionalBool(arguments, "comparePrevious") ?? false;
            var compareYear = McpToolArguments.OptionalBool(arguments, "compareYear") ?? false;
            var compareProjection = McpToolArguments.OptionalBool(arguments, "compareProjection") ?? false;
            var useValutaDate = McpToolArguments.OptionalBool(arguments, "useValutaDate") ?? false;
            var savingsPlanSource = McpToolArguments.OptionalEnum<SavingsPlanPostingSource>(arguments, "savingsPlanPostingSource") ?? SavingsPlanPostingSource.PlanPostings;

            var filters = new ReportAggregationFilters(
                AccountIds: McpToolArguments.OptionalGuidList(arguments, "accountIds"),
                ContactIds: McpToolArguments.OptionalGuidList(arguments, "contactIds"),
                SavingsPlanIds: McpToolArguments.OptionalGuidList(arguments, "savingsPlanIds"),
                SecurityIds: McpToolArguments.OptionalGuidList(arguments, "securityIds"),
                ContactCategoryIds: McpToolArguments.OptionalGuidList(arguments, "contactCategoryIds"),
                SavingsPlanCategoryIds: McpToolArguments.OptionalGuidList(arguments, "savingsPlanCategoryIds"),
                SecurityCategoryIds: McpToolArguments.OptionalGuidList(arguments, "securityCategoryIds"),
                SecuritySubTypes: McpToolArguments.OptionalIntList(arguments, "securitySubTypes"),
                IncludeDividendRelated: null,
                BankTextFilters: McpToolArguments.OptionalStringList(arguments, "bankTextFilters"),
                ContactTextFilters: McpToolArguments.OptionalStringList(arguments, "contactTextFilters"),
                SavingsPlanTextFilters: McpToolArguments.OptionalStringList(arguments, "savingsPlanTextFilters"),
                SecurityTextFilters: McpToolArguments.OptionalStringList(arguments, "securityTextFilters"));

            query = new ReportAggregationQuery(
                owner, postingKind, interval, take, includeCategory,
                comparePrevious, compareYear, compareProjection,
                new[] { postingKind }, null, useValutaDate, filters, savingsPlanSource);
        }

        var result = await _aggregation.QueryAsync(query, ct);

        var yearFilter = McpToolArguments.OptionalInt(arguments, "year");
        var points = result.Points.AsEnumerable();
        if (yearFilter.HasValue)
        {
            points = points.Where(p => p.PeriodStart.Year == yearFilter.Value);
        }
        var pointList = points.ToList();
        var periodStarts = pointList.Select(p => p.PeriodStart).Distinct().OrderBy(d => d).ToList();

        return new
        {
            favorite = favoriteOut,
            result = new
            {
                interval = result.Interval.ToString(),
                comparedPrevious = result.ComparedPrevious,
                comparedYear = result.ComparedYear,
                comparedProjection = result.ComparedProjection,
                coverage = new
                {
                    pointCount = pointList.Count,
                    periodCount = periodStarts.Count,
                    firstPeriod = periodStarts.FirstOrDefault(),
                    lastPeriod = periodStarts.LastOrDefault(),
                    yearFilter
                },
                points = pointList.Select(p => new
                {
                    periodStart = p.PeriodStart,
                    groupKey = p.GroupKey,
                    postingType = p.SecuritySubType?.ToString(),
                    amount = p.Amount,
                    projectionAmount = p.ProjectionAmount,
                    parentGroupKey = p.ParentGroupKey,
                    previousAmount = p.PreviousAmount,
                    yearAgoAmount = p.YearAgoAmount,
                    expectedDividends = p.ProjectionExpectedDividends?.Select(d => new
                    {
                        securityId = d.SecurityId,
                        expectedDate = d.ExpectedDate,
                        priorYearDate = d.PriorYearDate,
                        amount = d.Amount,
                        untrusted_data = new { securityName = d.SecurityName }
                    }).ToList(),
                    // Group/category names are resolved from master data names — untrusted
                    // content (sicherheitskonzept.md §5.2).
                    untrusted_data = new
                    {
                        groupName = p.GroupName,
                        categoryName = p.CategoryName
                    }
                }).ToList()
            }
        };
    }
}
