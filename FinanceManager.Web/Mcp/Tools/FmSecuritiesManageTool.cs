using FinanceManager.Application.Mcp;
using FinanceManager.Application.Securities;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Securities;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the write-preparing tool <c>fm_securities_manage</c> (T25): creates and
/// updates securities (<c>Identifier</c> WKN/ISIN, <c>AlphaVantageCode</c>,
/// <c>CurrencyCode</c>, <c>CategoryId</c>, region/sector, description). The
/// <c>archive</c> operation — the only delete-equivalent for securities
/// (fachkonzept §5.5: archiving instead of deletion; <c>DeleteAsync</c> is not
/// exposed) — is genehmigungspflichtig and answers
/// <c>Err_Mcp_ApprovalRequired</c> without effect until the approval queue ships
/// (EP-09).
/// Scopes: (Wertpapiere, Erstellung/Änderung), (Wertpapierkategorien, Lesen); archive
/// additionally (Wertpapiere, Löschung) — evaluated inside the handler.
/// </summary>
public sealed class FmSecuritiesManageDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_securities_manage";

    /// <summary>Creates the descriptor.</summary>
    public FmSecuritiesManageDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"operation\":{\"type\":\"string\",\"enum\":[\"create\",\"update\",\"archive\"],\"description\":\"Master-data operation (required); archive requires approval and has no effect until EP-09.\"},\"securityId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target security (required for update/archive).\"},\"name\":{\"type\":\"string\",\"description\":\"Security name (required for create; optional overwrite for update).\"},\"identifier\":{\"type\":\"string\",\"description\":\"WKN/ISIN identifier (required for create; optional overwrite for update).\"},\"description\":{\"type\":\"string\",\"description\":\"Free-text description; explicit null clears it on update.\"},\"alphaVantageCode\":{\"type\":\"string\",\"description\":\"Alpha-Vantage symbol; explicit null clears it on update.\"},\"currencyCode\":{\"type\":\"string\",\"description\":\"3-letter ISO-4217 currency (required for create; optional overwrite for update).\"},\"categoryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Category assignment; explicit null clears it on update.\"},\"region\":{\"type\":\"string\",\"description\":\"Region; explicit null clears it on update.\"},\"sector\":{\"type\":\"string\",\"description\":\"Sector; explicit null clears it on update.\"}},\"required\":[\"operation\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Creates and updates securities (name, identifier WKN/ISIN, alphaVantageCode, currencyCode, categoryId, region, sector, description). Archiving — the only delete-equivalent — requires approval and answers Err_Mcp_ApprovalRequired without effect until the approval queue ships (EP-09). Scopes: (Wertpapiere, Erstellung/Änderung), (Wertpapierkategorien, Lesen); archive additionally (Wertpapiere, Löschung).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Securities, McpRightKind.Create),
        new McpToolScope(McpDataType.Securities, McpRightKind.Modify),
        new McpToolScope(McpDataType.SecurityCategories, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_securities_manage</c> (F-0802): adapts <see cref="ISecurityService"/>.
/// Update merges the provided fields over the current state; explicit JSON null clears
/// the nullable metadata fields. Category references resolve owner-scoped via
/// <see cref="ISecurityCategoryService"/>; the record-level scope filter binds the
/// security's category. Each effect commits atomically with its staged
/// <c>DataChange</c> record (F-0301); archive answers approval-required without any
/// service call.
/// </summary>
public sealed class FmSecuritiesManageHandler : IMcpToolHandler
{
    private const string EntityType = "Security";

    private readonly ISecurityService _securities;
    private readonly ISecurityCategoryService _categories;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmSecuritiesManageHandler(
        ISecurityService securities,
        ISecurityCategoryService categories,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        McpRecordScopeFilter recordFilter)
    {
        _securities = securities;
        _categories = categories;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmSecuritiesManageDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var operation = McpToolArguments.OptionalString(arguments, "operation", 20)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'operation' is required.");

        return operation switch
        {
            "create" => await CreateAsync(arguments, owner, context, ct),
            "update" => await UpdateAsync(arguments, owner, context, ct),
            "archive" => await ArchiveAsync(arguments, owner, context, ct),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'operation' must be one of create|update|archive.")
        };
    }

    private async Task<object> CreateAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var name = McpMasterDataGuards.RequiredName(arguments);
        var identifier = McpToolArguments.OptionalString(arguments, "identifier", McpMasterDataGuards.MaxSecurityIdentifierLength);
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'identifier' is required for create.");
        }
        var currency = McpToolArguments.OptionalString(arguments, "currencyCode", 10)
            ?? "EUR";
        ValidateCurrency(currency);
        var description = McpToolArguments.OptionalString(arguments, "description", McpMasterDataGuards.MaxDescriptionLength);
        var alphaCode = McpToolArguments.OptionalString(arguments, "alphaVantageCode", McpMasterDataGuards.MaxAlphaVantageCodeLength);
        var region = McpToolArguments.OptionalString(arguments, "region", McpMasterDataGuards.MaxRegionSectorLength);
        var sector = McpToolArguments.OptionalString(arguments, "sector", McpMasterDataGuards.MaxRegionSectorLength);
        var categoryId = McpToolArguments.OptionalGuid(arguments, "categoryId");

        if (categoryId.HasValue)
        {
            await McpMasterDataGuards.RequireSecurityCategoryAsync(_categories, categoryId.Value, owner, ct);
        }
        await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, McpDataType.Securities, McpRightKind.Create,
            new McpRecordContext { SecurityCategoryId = categoryId }, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityType, Guid.Empty, "Created", null, $"{identifier}|{name}")
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "SecurityCreated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "create", name, identifier, currencyCode = currency, categoryId }), ct);
        SecurityDto created;
        try
        {
            created = await _securities.CreateAsync(owner, name, identifier, description, alphaCode, currency, categoryId, ct, region, sector);
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
        return new { operation = "create", applied = true, security = Result(created) };
    }

    private async Task<object> UpdateAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var securityId = McpToolArguments.RequiredGuid(arguments, "securityId");
        var existing = await _securities.GetAsync(securityId, owner, ct);
        if (existing is null)
        {
            return new { operation = "update", security = (object?)null };
        }
        if (!existing.IsActive)
        {
            throw new McpToolException("Err_Mcp_InvalidState",
                "The security is archived and cannot be updated.");
        }
        await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, McpDataType.Securities, McpRightKind.Modify,
            new McpRecordContext { SecurityCategoryId = existing.CategoryId }, ct);

        var effName = McpToolArguments.Has(arguments, "name")
            ? McpMasterDataGuards.RequiredName(arguments)
            : existing.Name;
        var effIdentifier = McpToolArguments.Has(arguments, "identifier")
            ? McpToolArguments.OptionalString(arguments, "identifier", McpMasterDataGuards.MaxSecurityIdentifierLength)
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'identifier' must not be null.")
            : existing.Identifier;
        var effCurrency = McpToolArguments.Has(arguments, "currencyCode")
            ? McpToolArguments.OptionalString(arguments, "currencyCode", 10)
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'currencyCode' must not be null.")
            : existing.CurrencyCode;
        var effDescription = McpToolArguments.Has(arguments, "description")
            ? McpToolArguments.OptionalString(arguments, "description", McpMasterDataGuards.MaxDescriptionLength)
            : existing.Description;
        var effAlpha = McpToolArguments.Has(arguments, "alphaVantageCode")
            ? McpToolArguments.OptionalString(arguments, "alphaVantageCode", McpMasterDataGuards.MaxAlphaVantageCodeLength)
            : existing.AlphaVantageCode;
        var effRegion = McpToolArguments.Has(arguments, "region")
            ? McpToolArguments.OptionalString(arguments, "region", McpMasterDataGuards.MaxRegionSectorLength)
            : existing.Region;
        var effSector = McpToolArguments.Has(arguments, "sector")
            ? McpToolArguments.OptionalString(arguments, "sector", McpMasterDataGuards.MaxRegionSectorLength)
            : existing.Sector;
        var effCategory = McpToolArguments.Has(arguments, "categoryId")
            ? McpToolArguments.OptionalGuid(arguments, "categoryId")
            : existing.CategoryId;

        if (string.IsNullOrWhiteSpace(effIdentifier))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'identifier' must not be empty.");
        }
        ValidateCurrency(effCurrency);
        if (effCategory.HasValue)
        {
            await McpMasterDataGuards.RequireSecurityCategoryAsync(_categories, effCategory.Value, owner, ct);
            await McpMasterDataGuards.RequireRecordWritableAsync(
                _recordFilter, context.Access, McpDataType.Securities, McpRightKind.Modify,
                new McpRecordContext { SecurityCategoryId = effCategory }, ct);
        }

        var changes = new List<McpAuditFieldChange>();
        McpMasterDataGuards.AddChange(changes, EntityType, securityId, "Name", existing.Name, effName);
        McpMasterDataGuards.AddChange(changes, EntityType, securityId, "Identifier", existing.Identifier, effIdentifier);
        McpMasterDataGuards.AddChange(changes, EntityType, securityId, "CurrencyCode", existing.CurrencyCode, effCurrency);
        McpMasterDataGuards.AddChange(changes, EntityType, securityId, "Description", existing.Description, effDescription);
        McpMasterDataGuards.AddChange(changes, EntityType, securityId, "AlphaVantageCode", existing.AlphaVantageCode, effAlpha);
        McpMasterDataGuards.AddChange(changes, EntityType, securityId, "Region", existing.Region, effRegion);
        McpMasterDataGuards.AddChange(changes, EntityType, securityId, "Sector", existing.Sector, effSector);
        McpMasterDataGuards.AddChange(changes, EntityType, securityId, "CategoryId", existing.CategoryId, effCategory);
        if (changes.Count == 0)
        {
            return new { operation = "update", applied = false, reason = "unchanged", security = Result(existing) };
        }

        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "SecurityUpdated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "update", securityId }), ct);
        SecurityDto? updated;
        try
        {
            updated = await _securities.UpdateAsync(securityId, owner, effName, effIdentifier, effDescription, effAlpha, effCurrency, effCategory, ct, effRegion, effSector);
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
            throw new McpToolException("Err_Mcp_InvalidState", "The security could not be updated.");
        }
        return new
        {
            operation = "update",
            applied = true,
            changedFields = changes.Select(c => c.Field).ToList(),
            security = Result(updated)
        };
    }

    private async Task<object> ArchiveAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var securityId = McpToolArguments.RequiredGuid(arguments, "securityId");
        var existing = await _securities.GetAsync(securityId, owner, ct);
        if (existing is null)
        {
            return new { operation = "archive", security = (object?)null };
        }
        return await McpMasterDataGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Securities, "archive",
            "Archives the security (IsActive=false) — the only delete-equivalent for " +
            "securities. The security stops matching new draft entries; existing " +
            "postings and price history keep their reference.",
            new { securityId }, ct);
    }

    private static void ValidateCurrency(string currency)
    {
        if (currency.Length != 3 || !currency.All(char.IsLetter))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'currencyCode' must be a 3-letter ISO-4217 code.");
        }
    }

    private static object Result(SecurityDto s)
        => new
        {
            id = s.Id,
            currencyCode = s.CurrencyCode,
            categoryId = s.CategoryId,
            isActive = s.IsActive,
            archivedUtc = s.ArchivedUtc,
            untrusted_data = new
            {
                name = s.Name,
                identifier = s.Identifier,
                description = s.Description,
                alphaVantageCode = s.AlphaVantageCode,
                region = s.Region,
                sector = s.Sector
            }
        };
}
