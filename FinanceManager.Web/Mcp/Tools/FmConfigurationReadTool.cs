using FinanceManager.Application.Mcp;
using FinanceManager.Application.Reports;
using FinanceManager.Domain.Mcp;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_configuration_read</c> (T10): reporting configuration —
/// report favorites (incl. filter id sets) and home-page KPI configuration.
/// Scopes: (Reports, Lesen), (KPI-Konfigurationen, Lesen).
/// </summary>
public sealed class FmConfigurationReadDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_configuration_read";

    /// <summary>Creates the descriptor.</summary>
    public FmConfigurationReadDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"favoriteId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single report favorite.\"},\"section\":{\"type\":\"string\",\"enum\":[\"favorites\",\"homeKpis\",\"all\"],\"description\":\"Which configuration sections to include (default 'all').\"}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Reads the reporting configuration: report favorites incl. filter sets and home-page KPIs. Scopes: (Reports, Lesen), (KPI-Konfigurationen, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Reports, McpRightKind.Read),
        new McpToolScope(McpDataType.KpiConfigurations, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_configuration_read</c>: adapts <see cref="IReportFavoriteService"/> and
/// <see cref="IHomeKpiService"/>. Configuration names/titles are untrusted content
/// (<c>untrusted_data</c>); id-typed filter sets are structured data. Foreign favorites
/// behave like non-existing ones (null result).
/// </summary>
public sealed class FmConfigurationReadHandler : IMcpToolHandler
{
    private readonly IReportFavoriteService _favorites;
    private readonly IHomeKpiService _homeKpis;

    /// <summary>Creates the handler.</summary>
    public FmConfigurationReadHandler(IReportFavoriteService favorites, IHomeKpiService homeKpis)
    {
        _favorites = favorites;
        _homeKpis = homeKpis;
    }

    /// <inheritdoc />
    public string ToolName => FmConfigurationReadDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var favoriteId = McpToolArguments.OptionalGuid(arguments, "favoriteId");
        var section = McpToolArguments.OptionalString(arguments, "section", 20) ?? "all";
        if (section is not ("favorites" or "homeKpis" or "all"))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'section' must be one of 'favorites', 'homeKpis', 'all'.");
        }

        object? favorites = null;
        if (section is "favorites" or "all")
        {
            if (favoriteId.HasValue)
            {
                var f = await _favorites.GetAsync(favoriteId.Value, owner, ct);
                favorites = f is null ? null : new[] { MapFavorite(f) };
            }
            else
            {
                favorites = (await _favorites.ListAsync(owner, ct)).Select(MapFavorite).ToList();
            }
        }

        object? homeKpis = null;
        if (section is "homeKpis" or "all")
        {
            homeKpis = (await _homeKpis.ListAsync(owner, ct))
                .Select(k => new
                {
                    id = k.Id,
                    kind = k.Kind.ToString(),
                    reportFavoriteId = k.ReportFavoriteId,
                    predefinedType = k.PredefinedType?.ToString(),
                    displayMode = k.DisplayMode.ToString(),
                    sortOrder = k.SortOrder,
                    createdUtc = k.CreatedUtc,
                    modifiedUtc = k.ModifiedUtc,
                    untrusted_data = new
                    {
                        title = k.Title,
                        reportFavoriteName = k.ReportFavoriteName
                    }
                }).ToList();
        }

        return new { favorites, homeKpis };
    }

    private static object MapFavorite(FinanceManager.Shared.Dtos.Reports.ReportFavoriteDto f)
        => new
        {
            id = f.Id,
            postingKind = f.PostingKind.ToString(),
            postingKinds = f.PostingKinds.Select(k => k.ToString()).ToList(),
            interval = f.Interval.ToString(),
            take = f.Take,
            includeCategory = f.IncludeCategory,
            comparePrevious = f.ComparePrevious,
            compareYear = f.CompareYear,
            compareProjection = f.CompareProjection,
            showChart = f.ShowChart,
            expandable = f.Expandable,
            useValutaDate = f.UseValutaDate,
            createdUtc = f.CreatedUtc,
            modifiedUtc = f.ModifiedUtc,
            filters = f.Filters is null ? null : new
            {
                accountIds = f.Filters.AccountIds,
                contactIds = f.Filters.ContactIds,
                savingsPlanIds = f.Filters.SavingsPlanIds,
                securityIds = f.Filters.SecurityIds,
                contactCategoryIds = f.Filters.ContactCategoryIds,
                savingsPlanCategoryIds = f.Filters.SavingsPlanCategoryIds,
                securityCategoryIds = f.Filters.SecurityCategoryIds,
                securitySubTypes = f.Filters.SecuritySubTypes,
                includeDividendRelated = f.Filters.IncludeDividendRelated
            },
            untrusted_data = new { name = f.Name }
        };
}
