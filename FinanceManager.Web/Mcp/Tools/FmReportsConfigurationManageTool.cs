using FinanceManager.Application.Mcp;
using FinanceManager.Application.Reports;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.HomeKpi;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Reports;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the write-preparing tool <c>fm_reports_configuration_manage</c> (T29):
/// maintains report favorites (<c>ReportFavorite</c> incl. filter sets) and home-page
/// KPIs (<c>HomeKpi</c> incl. predefined/favorite references). The delete modes are
/// genehmigungspflichtig and answer <c>Err_Mcp_ApprovalRequired</c> without effect until
/// the approval queue ships (EP-09).
/// Scopes: (Reports, Erstellung/Änderung), (KPI-Konfigurationen,
/// Erstellung/Änderung); deletes additionally the Löschung right of the entity
/// family — evaluated inside the handler.
/// </summary>
public sealed class FmReportsConfigurationManageDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_reports_configuration_manage";

    private const string JsonSchema = "{\"type\":\"object\",\"properties\":{\"entity\":{\"type\":\"string\",\"enum\":[\"reportFavorite\",\"homeKpi\"],\"description\":\"Configuration entity family (required).\"},\"operation\":{\"type\":\"string\",\"enum\":[\"create\",\"update\",\"delete\"],\"description\":\"Operation (required); delete requires approval and has no effect until EP-09.\"},\"favoriteId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target report favorite (required for entity=reportFavorite update/delete).\"},\"kpiId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target home KPI (required for entity=homeKpi update/delete).\"},\"name\":{\"type\":\"string\",\"description\":\"Favorite name (required for reportFavorite create; optional overwrite for update).\"},\"postingKind\":{\"type\":\"string\",\"enum\":[\"Bank\",\"Contact\",\"SavingsPlan\",\"Security\"],\"description\":\"Primary posting kind (required for reportFavorite create).\"},\"postingKinds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"enum\":[\"Bank\",\"Contact\",\"SavingsPlan\",\"Security\"]},\"description\":\"Multi posting-kind filter set; explicit null resets it to the primary postingKind on update.\"},\"includeCategory\":{\"type\":\"boolean\"},\"interval\":{\"type\":\"string\",\"enum\":[\"Month\",\"Quarter\",\"HalfYear\",\"Year\",\"Ytd\",\"AllHistory\"],\"description\":\"Report interval (required for reportFavorite create).\"},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":120,\"description\":\"Result window size 1-120 (domain bound of ReportFavorite.SetTake — the domain clamps, the tool validates).\"},\"comparePrevious\":{\"type\":\"boolean\"},\"compareYear\":{\"type\":\"boolean\"},\"compareProjection\":{\"type\":\"boolean\"},\"showChart\":{\"type\":\"boolean\"},\"expandable\":{\"type\":\"boolean\"},\"useValutaDate\":{\"type\":\"boolean\"},\"filters\":{\"type\":\"object\",\"properties\":{\"accountIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"contactIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"savingsPlanIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"securityIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"contactCategoryIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"savingsPlanCategoryIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"securityCategoryIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"}},\"securitySubTypes\":{\"type\":\"array\",\"items\":{\"type\":\"integer\"}},\"includeDividendRelated\":{\"type\":\"boolean\"}},\"additionalProperties\":false,\"description\":\"Favorite filter sets; explicit null clears them on update.\"},\"kind\":{\"type\":\"string\",\"enum\":[\"Predefined\",\"ReportFavorite\"],\"description\":\"HomeKpi kind (required for homeKpi create).\"},\"reportFavoriteId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Favorite reference (required when kind=ReportFavorite); explicit null clears it on update.\"},\"predefinedType\":{\"type\":\"string\",\"description\":\"Predefined KPI type (required when kind=Predefined): AccountsAggregates, SavingsPlanAggregates, SecuritiesDividends, MonthlyBudget, ActiveSavingsPlansCount, ContactsCount, SecuritiesCount, OpenStatementDraftsCount; explicit null clears it on update.\"},\"title\":{\"type\":\"string\",\"description\":\"Display title (max 120 characters — domain limit of HomeKpi.SetTitle; the domain truncates, the tool rejects); explicit null clears it on update.\"},\"displayMode\":{\"type\":\"string\",\"enum\":[\"TotalOnly\",\"TotalWithComparisons\",\"ReportGraph\"],\"description\":\"Display mode (required for homeKpi create).\"},\"sortOrder\":{\"type\":\"integer\",\"description\":\"Sort position (default 0).\"}},\"required\":[\"entity\",\"operation\"],\"additionalProperties\":false}";

    /// <summary>Creates the descriptor.</summary>
    public FmReportsConfigurationManageDescriptor()
        : base(JsonSchema)
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Maintains report configuration: report favorites (entity=reportFavorite, incl. posting kinds, interval, comparison flags, filter sets) and home-page KPIs (entity=homeKpi, kind Predefined or ReportFavorite with title/displayMode/sortOrder). Delete operations require approval and answer Err_Mcp_ApprovalRequired without effect until the approval queue ships (EP-09). Scopes: (Reports, Erstellung/Änderung), (KPI-Konfigurationen, Erstellung/Änderung); deletes additionally the Löschung right of the entity family.";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Reports, McpRightKind.Create),
        new McpToolScope(McpDataType.Reports, McpRightKind.Modify),
        new McpToolScope(McpDataType.KpiConfigurations, McpRightKind.Create),
        new McpToolScope(McpDataType.KpiConfigurations, McpRightKind.Modify)
    };
}

/// <summary>
/// Handler of <c>fm_reports_configuration_manage</c> (F-0803): adapts
/// <see cref="IReportFavoriteService"/> and <see cref="IHomeKpiService"/>.
/// Update operations merge provided fields over the current state (the update request
/// records carry the full field set). A <c>ReportFavorite</c>-KPI validates its
/// favorite reference owner-scoped; a <c>Predefined</c>-KPI requires a defined
/// <c>predefinedType</c>. The record-level write filter evaluates against an
/// unclassified record (filtered write rows deny). Each effect commits atomically with
/// its staged <c>DataChange</c> record (F-0301); deletes answer the structured
/// approval-required result without any service call.
/// </summary>
public sealed class FmReportsConfigurationManageHandler : IMcpToolHandler
{
    private const string EntityFavorite = "ReportFavorite";
    private const string EntityKpi = "HomeKpi";

    /// <summary>Domain bound of <c>ReportFavorite.SetTake</c> — the domain clamps to
    /// 1–120, the tool validates instead of silently truncating.</summary>
    private const int MaxFavoriteTake = 120;

    private readonly IReportFavoriteService _favorites;
    private readonly IHomeKpiService _kpis;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    /// <param name="favorites">Report favorite service for favorite CRUD.</param>
    /// <param name="kpis">Home KPI service for KPI CRUD.</param>
    /// <param name="scopeEvaluator">Evaluator for MCP scope checks.</param>
    /// <param name="auditWriter">Audit writer used for staged change tracking.</param>
    /// <param name="recordFilter">Record-level scope filter.</param>
    public FmReportsConfigurationManageHandler(
        IReportFavoriteService favorites,
        IHomeKpiService kpis,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        McpRecordScopeFilter recordFilter)
    {
        _favorites = favorites;
        _kpis = kpis;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmReportsConfigurationManageDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var entity = McpToolArguments.OptionalString(arguments, "entity", 20)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'entity' is required.");
        var operation = McpToolArguments.OptionalString(arguments, "operation", 20)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'operation' is required.");

        return (entity, operation) switch
        {
            ("reportFavorite", "create") => await CreateFavoriteAsync(arguments, owner, context, ct),
            ("reportFavorite", "update") => await UpdateFavoriteAsync(arguments, owner, context, ct),
            ("reportFavorite", "delete") => await DeleteFavoriteAsync(arguments, owner, context, ct),
            ("homeKpi", "create") => await CreateKpiAsync(arguments, owner, context, ct),
            ("homeKpi", "update") => await UpdateKpiAsync(arguments, owner, context, ct),
            ("homeKpi", "delete") => await DeleteKpiAsync(arguments, owner, context, ct),
            ("reportFavorite" or "homeKpi", _) => throw new McpToolException(
                "Err_Mcp_InvalidArguments", "Argument 'operation' must be one of create|update|delete."),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'entity' must be one of reportFavorite|homeKpi.")
        };
    }

    // ------------------------------------------------------------------
    // reportFavorite
    // ------------------------------------------------------------------

    private async Task<object> CreateFavoriteAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var name = McpMasterDataGuards.RequiredName(arguments);
        var postingKind = McpToolArguments.OptionalEnum<PostingKind>(arguments, "postingKind")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'postingKind' is required for reportFavorite create.");
        var interval = McpToolArguments.OptionalEnum<ReportInterval>(arguments, "interval")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'interval' is required for reportFavorite create.");
        var request = FavoriteRequest(arguments, name, postingKind, interval);
        await RequireWritableAsync(context, McpDataType.Reports, McpRightKind.Create, ct);

        // Create first, then stage the DataChange with the real record id — the audit
        // writer rejects placeholder entity ids (Guid.Empty).
        ReportFavoriteDto created;
        try
        {
            created = await _favorites.CreateAsync(owner, request, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        var changes = new List<McpAuditFieldChange>
        {
            new(EntityFavorite, created.Id, "Created", null, $"{postingKind}|{interval}|{name}")
        };
        await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "ReportFavoriteCreated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { entity = "reportFavorite", operation = "create", name, postingKind = postingKind.ToString(), interval = interval.ToString() }), ct);
        return new { entity = "reportFavorite", operation = "create", applied = true, favorite = FavoriteResult(created) };
    }

    private async Task<object> UpdateFavoriteAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var favoriteId = McpToolArguments.RequiredGuid(arguments, "favoriteId");
        var existing = await _favorites.GetAsync(favoriteId, owner, ct);
        if (existing is null)
        {
            return new { entity = "reportFavorite", operation = "update", favorite = (object?)null };
        }
        await RequireWritableAsync(context, McpDataType.Reports, McpRightKind.Modify, ct);

        var effName = McpToolArguments.Has(arguments, "name")
            ? McpMasterDataGuards.RequiredName(arguments)
            : existing.Name;
        var effKind = McpToolArguments.Has(arguments, "postingKind")
            ? McpToolArguments.OptionalEnum<PostingKind>(arguments, "postingKind")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'postingKind' must not be null.")
            : existing.PostingKind;
        var effInterval = McpToolArguments.Has(arguments, "interval")
            ? McpToolArguments.OptionalEnum<ReportInterval>(arguments, "interval")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'interval' must not be null.")
            : existing.Interval;
        var effTake = McpToolArguments.Has(arguments, "take")
            ? McpToolArguments.OptionalInt(arguments, "take")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'take' must not be null.")
            : existing.Take;
        if (effTake is < 1 or > MaxFavoriteTake)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Argument 'take' must be between 1 and {MaxFavoriteTake}.");
        }
        var effIncludeCategory = Flag(arguments, "includeCategory", existing.IncludeCategory);
        var effComparePrevious = Flag(arguments, "comparePrevious", existing.ComparePrevious);
        var effCompareYear = Flag(arguments, "compareYear", existing.CompareYear);
        var effCompareProjection = Flag(arguments, "compareProjection", existing.CompareProjection);
        var effShowChart = Flag(arguments, "showChart", existing.ShowChart);
        var effExpandable = Flag(arguments, "expandable", existing.Expandable);
        var effUseValuta = Flag(arguments, "useValutaDate", existing.UseValutaDate);
        var effSavingsPlanSource = McpToolArguments.Has(arguments, "savingsPlanPostingSource")
            ? McpToolArguments.OptionalEnum<SavingsPlanPostingSource>(arguments, "savingsPlanPostingSource")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'savingsPlanPostingSource' must not be null.")
            : existing.SavingsPlanPostingSource;
        var effKinds = McpToolArguments.Has(arguments, "postingKinds")
            ? ParsePostingKinds(arguments)
            : existing.PostingKinds;
        var effFilters = McpToolArguments.Has(arguments, "filters")
            ? ParseFilters(arguments.GetProperty("filters"))
            : existing.Filters;

        var changes = new List<McpAuditFieldChange>();
        McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "Name", existing.Name, effName);
        McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "PostingKind", existing.PostingKind, effKind);
        McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "Interval", existing.Interval, effInterval);
        McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "Take", existing.Take, effTake);
        McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "IncludeCategory", existing.IncludeCategory, effIncludeCategory);
        McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "ComparePrevious", existing.ComparePrevious, effComparePrevious);
        McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "CompareYear", existing.CompareYear, effCompareYear);
        McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "CompareProjection", existing.CompareProjection, effCompareProjection);
        McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "ShowChart", existing.ShowChart, effShowChart);
        McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "Expandable", existing.Expandable, effExpandable);
        McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "UseValutaDate", existing.UseValutaDate, effUseValuta);
        McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "SavingsPlanPostingSource", existing.SavingsPlanPostingSource, effSavingsPlanSource);
        if (McpToolArguments.Has(arguments, "postingKinds"))
        {
            McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "PostingKinds",
                string.Join(',', existing.PostingKinds), string.Join(',', effKinds ?? Array.Empty<PostingKind>()));
        }
        if (McpToolArguments.Has(arguments, "filters"))
        {
            McpMasterDataGuards.AddChange(changes, EntityFavorite, favoriteId, "Filters", "(set)", "(updated)");
        }
        if (changes.Count == 0)
        {
            return new { entity = "reportFavorite", operation = "update", applied = false, reason = "unchanged", favorite = FavoriteResult(existing) };
        }

        var request = new ReportFavoriteUpdateRequest(
            effName, effKind, effIncludeCategory, effInterval, effTake,
            effComparePrevious, effCompareYear, effCompareProjection, effShowChart, effExpandable,
            effKinds, effFilters, effUseValuta, effSavingsPlanSource);
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "ReportFavoriteUpdated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { entity = "reportFavorite", operation = "update", favoriteId }), ct);
        ReportFavoriteDto? updated;
        try
        {
            updated = await _favorites.UpdateAsync(favoriteId, owner, request, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        if (updated is null)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidState", "The report favorite could not be updated.");
        }
        return new
        {
            entity = "reportFavorite",
            operation = "update",
            applied = true,
            changedFields = changes.Select(c => c.Field).ToList(),
            favorite = FavoriteResult(updated)
        };
    }

    private async Task<object> DeleteFavoriteAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var favoriteId = McpToolArguments.RequiredGuid(arguments, "favoriteId");
        var existing = await _favorites.GetAsync(favoriteId, owner, ct);
        if (McpMasterDataGuards.ThrowIfExecutionTargetGone(context, existing, "delete"))
        {
            return new { entity = "reportFavorite", operation = "delete", favorite = (object?)null };
        }
        return await McpMasterDataGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Reports, "delete",
            "Deletes the report favorite permanently; home KPIs referencing it lose " +
            "their favorite link.",
            new { entity = "reportFavorite", favoriteId }, ct,
            executeApproved: async ct2 =>
            {
                var changes = new List<McpAuditFieldChange>
                {
                    new(EntityFavorite, favoriteId, "Deleted", existing!.Name, null)
                };
                var staged = await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "ReportFavoriteDeleted", ToolName, changes, 1,
                    JsonSerializer.Serialize(new { entity = "reportFavorite", operation = "delete", favoriteId }), ct2);
                if (!await _favorites.DeleteAsync(favoriteId, owner, ct2))
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw new McpToolException("Err_TargetGone", "The report favorite no longer exists.");
                }
                return (object)new { entity = "reportFavorite", operation = "delete", applied = true, favoriteId };
            });
    }

    // ------------------------------------------------------------------
    // homeKpi
    // ------------------------------------------------------------------

    private async Task<object> CreateKpiAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var kind = McpToolArguments.OptionalEnum<HomeKpiKind>(arguments, "kind")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'kind' is required for homeKpi create.");
        var displayMode = McpToolArguments.OptionalEnum<HomeKpiDisplayMode>(arguments, "displayMode")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'displayMode' is required for homeKpi create.");
        var favoriteId = McpToolArguments.OptionalGuid(arguments, "reportFavoriteId");
        var predefined = McpToolArguments.OptionalEnum<HomeKpiPredefined>(arguments, "predefinedType");
        var title = McpToolArguments.OptionalString(arguments, "title", McpMasterDataGuards.MaxTitleLength);
        var sortOrder = McpToolArguments.OptionalInt(arguments, "sortOrder") ?? 0;

        await ValidateKpiKindAsync(kind, favoriteId, predefined, owner, ct);
        await RequireWritableAsync(context, McpDataType.KpiConfigurations, McpRightKind.Create, ct);

        var request = new HomeKpiCreateRequest(kind, favoriteId, predefined, title, displayMode, sortOrder);
        // Create first, then stage the DataChange with the real record id — the audit
        // writer rejects placeholder entity ids (Guid.Empty).
        HomeKpiDto created;
        try
        {
            created = await _kpis.CreateAsync(owner, request, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        var changes = new List<McpAuditFieldChange>
        {
            new(EntityKpi, created.Id, "Created", null, $"{kind}|{displayMode}|{sortOrder}")
        };
        await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "HomeKpiCreated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { entity = "homeKpi", operation = "create", kind = kind.ToString(), reportFavoriteId = favoriteId, predefinedType = predefined?.ToString() }), ct);
        return new { entity = "homeKpi", operation = "create", applied = true, homeKpi = KpiResult(created) };
    }

    private async Task<object> UpdateKpiAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var kpiId = McpToolArguments.RequiredGuid(arguments, "kpiId");
        var existing = (await _kpis.ListAsync(owner, ct)).FirstOrDefault(k => k.Id == kpiId);
        if (existing is null)
        {
            return new { entity = "homeKpi", operation = "update", homeKpi = (object?)null };
        }
        await RequireWritableAsync(context, McpDataType.KpiConfigurations, McpRightKind.Modify, ct);

        var effKind = McpToolArguments.Has(arguments, "kind")
            ? McpToolArguments.OptionalEnum<HomeKpiKind>(arguments, "kind")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'kind' must not be null.")
            : existing.Kind;
        var effDisplay = McpToolArguments.Has(arguments, "displayMode")
            ? McpToolArguments.OptionalEnum<HomeKpiDisplayMode>(arguments, "displayMode")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'displayMode' must not be null.")
            : existing.DisplayMode;
        var effFavorite = McpToolArguments.Has(arguments, "reportFavoriteId")
            ? McpToolArguments.OptionalGuid(arguments, "reportFavoriteId")
            : existing.ReportFavoriteId;
        var effPredefined = McpToolArguments.Has(arguments, "predefinedType")
            ? McpToolArguments.OptionalEnum<HomeKpiPredefined>(arguments, "predefinedType")
            : existing.PredefinedType;
        var effTitle = McpToolArguments.Has(arguments, "title")
            ? McpToolArguments.OptionalString(arguments, "title", McpMasterDataGuards.MaxTitleLength)
            : existing.Title;
        var effSort = McpToolArguments.Has(arguments, "sortOrder")
            ? McpToolArguments.OptionalInt(arguments, "sortOrder")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'sortOrder' must not be null.")
            : existing.SortOrder;

        await ValidateKpiKindAsync(effKind, effFavorite, effPredefined, owner, ct);

        var changes = new List<McpAuditFieldChange>();
        McpMasterDataGuards.AddChange(changes, EntityKpi, kpiId, "Kind", existing.Kind, effKind);
        McpMasterDataGuards.AddChange(changes, EntityKpi, kpiId, "ReportFavoriteId", existing.ReportFavoriteId, effFavorite);
        McpMasterDataGuards.AddChange(changes, EntityKpi, kpiId, "PredefinedType", existing.PredefinedType, effPredefined);
        McpMasterDataGuards.AddChange(changes, EntityKpi, kpiId, "Title", existing.Title, effTitle);
        McpMasterDataGuards.AddChange(changes, EntityKpi, kpiId, "DisplayMode", existing.DisplayMode, effDisplay);
        McpMasterDataGuards.AddChange(changes, EntityKpi, kpiId, "SortOrder", existing.SortOrder, effSort);
        if (changes.Count == 0)
        {
            return new { entity = "homeKpi", operation = "update", applied = false, reason = "unchanged", homeKpi = KpiResult(existing) };
        }

        var request = new HomeKpiUpdateRequest(effKind, effFavorite, effPredefined, effTitle, effDisplay, effSort);
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "HomeKpiUpdated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { entity = "homeKpi", operation = "update", kpiId }), ct);
        HomeKpiDto? updated;
        try
        {
            updated = await _kpis.UpdateAsync(kpiId, owner, request, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        if (updated is null)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidState", "The home KPI could not be updated.");
        }
        return new
        {
            entity = "homeKpi",
            operation = "update",
            applied = true,
            changedFields = changes.Select(c => c.Field).ToList(),
            homeKpi = KpiResult(updated)
        };
    }

    private async Task<object> DeleteKpiAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var kpiId = McpToolArguments.RequiredGuid(arguments, "kpiId");
        var existing = (await _kpis.ListAsync(owner, ct)).FirstOrDefault(k => k.Id == kpiId);
        if (McpMasterDataGuards.ThrowIfExecutionTargetGone(context, existing, "delete"))
        {
            return new { entity = "homeKpi", operation = "delete", homeKpi = (object?)null };
        }
        return await McpMasterDataGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.KpiConfigurations, "delete",
            "Deletes the home KPI configuration entry permanently; it disappears from " +
            "the owner's dashboard.",
            new { entity = "homeKpi", kpiId }, ct,
            executeApproved: async ct2 =>
            {
                var changes = new List<McpAuditFieldChange>
                {
                    new(EntityKpi, kpiId, "Deleted", existing!.Title ?? kpiId.ToString(), null)
                };
                var staged = await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "HomeKpiDeleted", ToolName, changes, 1,
                    JsonSerializer.Serialize(new { entity = "homeKpi", operation = "delete", kpiId }), ct2);
                if (!await _kpis.DeleteAsync(kpiId, owner, ct2))
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw new McpToolException("Err_TargetGone", "The home KPI no longer exists.");
                }
                return (object)new { entity = "homeKpi", operation = "delete", applied = true, kpiId };
            });
    }

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------

    private async Task RequireWritableAsync(McpToolCallContext context, McpDataType dataType, McpRightKind right, CancellationToken ct)
        => await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, dataType, right, new McpRecordContext(), ct);

    private async Task ValidateKpiKindAsync(
        HomeKpiKind kind, Guid? favoriteId, HomeKpiPredefined? predefined, Guid owner, CancellationToken ct)
    {
        switch (kind)
        {
            case HomeKpiKind.ReportFavorite:
                if (!favoriteId.HasValue)
                {
                    throw new McpToolException("Err_Mcp_InvalidArguments",
                        "Argument 'reportFavoriteId' is required when kind=ReportFavorite.");
                }
                _ = await _favorites.GetAsync(favoriteId.Value, owner, ct)
                    ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                        "The referenced report favorite does not exist or is not owned by the caller.");
                break;
            case HomeKpiKind.Predefined:
                if (predefined is null)
                {
                    throw new McpToolException("Err_Mcp_InvalidArguments",
                        "Argument 'predefinedType' is required when kind=Predefined.");
                }
                break;
        }
    }

    private static bool Flag(JsonElement arguments, string name, bool fallback)
        => McpToolArguments.Has(arguments, name)
            ? McpToolArguments.OptionalBool(arguments, name)
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{name}' must not be null.")
            : fallback;

    private static ReportFavoriteCreateRequest FavoriteRequest(
        JsonElement arguments, string name, PostingKind postingKind, ReportInterval interval)
    {
        var take = McpToolArguments.OptionalInt(arguments, "take") ?? 24;
        if (take is < 1 or > MaxFavoriteTake)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Argument 'take' must be between 1 and {MaxFavoriteTake}.");
        }
        return new ReportFavoriteCreateRequest(
            name,
            postingKind,
            McpToolArguments.OptionalBool(arguments, "includeCategory") ?? false,
            interval,
            take,
            McpToolArguments.OptionalBool(arguments, "comparePrevious") ?? false,
            McpToolArguments.OptionalBool(arguments, "compareYear") ?? false,
            McpToolArguments.OptionalBool(arguments, "compareProjection") ?? false,
            McpToolArguments.OptionalBool(arguments, "showChart") ?? false,
            McpToolArguments.OptionalBool(arguments, "expandable") ?? false,
            McpToolArguments.Has(arguments, "postingKinds") ? ParsePostingKinds(arguments) : null,
            McpToolArguments.Has(arguments, "filters") ? ParseFilters(arguments.GetProperty("filters")) : null,
            McpToolArguments.OptionalBool(arguments, "useValutaDate") ?? false,
            McpToolArguments.OptionalEnum<SavingsPlanPostingSource>(arguments, "savingsPlanPostingSource") ?? SavingsPlanPostingSource.PlanPostings);
    }

    private static IReadOnlyCollection<PostingKind>? ParsePostingKinds(JsonElement arguments)
    {
        // Absent or explicit JSON null (clears/resets the multi-kind set on update).
        if (!McpToolArguments.HasNonNull(arguments, "postingKinds"))
        {
            return null;
        }
        var el = arguments.GetProperty("postingKinds");
        if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() > 20)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'postingKinds' must be an array of at most 20 PostingKind names.");
        }
        var list = new List<PostingKind>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String
                || !Enum.TryParse<PostingKind>(item.GetString(), ignoreCase: true, out var kind)
                || !Enum.IsDefined(kind))
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    "Argument 'postingKinds' must contain PostingKind names only.");
            }
            list.Add(kind);
        }
        return list;
    }

    private static ReportFavoriteFiltersDto? ParseFilters(JsonElement filters)
    {
        if (filters.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        if (filters.ValueKind != JsonValueKind.Object)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'filters' must be an object.");
        }
        return new ReportFavoriteFiltersDto(
            McpToolArguments.OptionalGuidList(filters, "accountIds"),
            McpToolArguments.OptionalGuidList(filters, "contactIds"),
            McpToolArguments.OptionalGuidList(filters, "savingsPlanIds"),
            McpToolArguments.OptionalGuidList(filters, "securityIds"),
            McpToolArguments.OptionalGuidList(filters, "contactCategoryIds"),
            McpToolArguments.OptionalGuidList(filters, "savingsPlanCategoryIds"),
            McpToolArguments.OptionalGuidList(filters, "securityCategoryIds"),
            McpToolArguments.OptionalIntList(filters, "securitySubTypes"),
            McpToolArguments.OptionalBool(filters, "includeDividendRelated"));
    }

    private static object FavoriteResult(ReportFavoriteDto f)
        => new
        {
            id = f.Id,
            postingKind = f.PostingKind.ToString(),
            postingKinds = f.PostingKinds.Select(k => k.ToString()).ToList(),
            includeCategory = f.IncludeCategory,
            interval = f.Interval.ToString(),
            take = f.Take,
            comparePrevious = f.ComparePrevious,
            compareYear = f.CompareYear,
            compareProjection = f.CompareProjection,
            showChart = f.ShowChart,
            expandable = f.Expandable,
            useValutaDate = f.UseValutaDate,
            savingsPlanPostingSource = f.SavingsPlanPostingSource.ToString(),
            createdUtc = f.CreatedUtc,
            modifiedUtc = f.ModifiedUtc,
            untrusted_data = new { name = f.Name }
        };

    private static object KpiResult(HomeKpiDto k)
        => new
        {
            id = k.Id,
            kind = k.Kind.ToString(),
            reportFavoriteId = k.ReportFavoriteId,
            predefinedType = k.PredefinedType?.ToString(),
            displayMode = k.DisplayMode.ToString(),
            sortOrder = k.SortOrder,
            createdUtc = k.CreatedUtc,
            modifiedUtc = k.ModifiedUtc,
            untrusted_data = new { title = k.Title, reportFavoriteName = k.ReportFavoriteName }
        };
}
