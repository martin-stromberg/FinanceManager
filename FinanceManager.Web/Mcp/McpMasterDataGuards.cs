using FinanceManager.Application.Attachments;
using FinanceManager.Application.Budget;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Domain.Attachments;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Attachments;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Securities;
using System.Globalization;
using System.Text.Json;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// MCP-specific fachliche Zusatzvalidierung of the master-data write tools T22–T29 (EP-08,
/// scope-und-toolkatalog.md §3.4): owner-scoped foreign-reference resolution (a foreign id
/// behaves like a missing record), plausibility rules for names/texts/URLs, the
/// record-level write-scope check on filtered scope rows (EP-05) and the structured
/// <c>Err_Mcp_ApprovalRequired</c> answer for the final/destructive sub-operations
/// (merge, delete, archive) whose queue coupling ships with EP-09 — the answer carries a
/// description of the effect and produces <b>no</b> business effect.
/// All checks run <b>before</b> any service effect so a rejected call leaves no half state.
/// </summary>
internal static class McpMasterDataGuards
{
    /// <summary>Maximum accepted master-data name length (category/contact/account names).</summary>
    internal const int MaxNameLength = 150;

    /// <summary>Maximum accepted free-text description length.</summary>
    internal const int MaxDescriptionLength = 1000;

    /// <summary>Maximum accepted IBAN length (ISO 13616).</summary>
    internal const int MaxIbanLength = 34;

    /// <summary>Maximum accepted alias-pattern length.</summary>
    internal const int MaxAliasPatternLength = 250;

    /// <summary>Maximum accepted contract-number length.</summary>
    internal const int MaxContractNumberLength = 100;

    /// <summary>Maximum accepted security identifier (WKN/ISIN) length.</summary>
    internal const int MaxSecurityIdentifierLength = 50;

    /// <summary>Maximum accepted alpha-vantage code length.</summary>
    internal const int MaxAlphaVantageCodeLength = 50;

    /// <summary>Maximum accepted region/sector length — aligned to the domain limit of
    /// <c>Security.Update</c> (255 characters).</summary>
    internal const int MaxRegionSectorLength = 255;

    /// <summary>Maximum accepted budget purpose-pattern length.</summary>
    internal const int MaxPurposePatternLength = 500;

    /// <summary>Maximum accepted attachment file-name length.</summary>
    internal const int MaxFileNameLength = 250;

    /// <summary>Maximum accepted content-type length.</summary>
    internal const int MaxContentTypeLength = 100;

    /// <summary>Maximum accepted URL length.</summary>
    internal const int MaxUrlLength = 2000;

    /// <summary>Maximum accepted KPI title length — aligned to the domain limit of
    /// <c>HomeKpi.SetTitle</c> (120 characters; the domain would silently truncate, the
    /// tool rejects instead).</summary>
    internal const int MaxTitleLength = 120;

    /// <summary>
    /// Maximum accepted raw upload size for one MCP attachment upload. The JSON-RPC
    /// envelope is capped at 1 MiB (McpEndpointExtensions.MaxBodyBytes), so a base64
    /// payload can never transport more than ~700 KiB raw; 512 KiB leaves headroom for
    /// the remaining arguments.
    /// </summary>
    internal const long MaxUploadBytes = 512L * 1024L;

    // ------------------------------------------------------------------
    // Scope helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Applies the record-level write filter of the effective scope profile (EP-05):
    /// when at least one matching (dataType, right) scope row carries a filter CSV, the
    /// record must match a whitelisted classification value; records without the
    /// classification dimension are unclassified and denied by a filtered row.
    /// Unfiltered profiles pass without per-record work.
    /// </summary>
    /// <param name="recordFilter">The request-scoped record filter.</param>
    /// <param name="access">The MCP access context.</param>
    /// <param name="dataType">Data type of the affected record.</param>
    /// <param name="right">The exercised write right.</param>
    /// <param name="record">Classification values of the affected record.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="McpToolException">The filtered scope does not cover the record.</exception>
    public static async Task RequireRecordWritableAsync(
        McpRecordScopeFilter recordFilter, IMcpAccessContext access,
        McpDataType dataType, McpRightKind right, McpRecordContext record, CancellationToken ct)
    {
        var scopes = await recordFilter.GetScopesAsync(access, dataType, right, ct);
        if (McpRecordScopeFilter.RequiresRecordFilter(scopes)
            && !McpRecordScopeFilter.IsAllowed(scopes, record))
        {
            throw new McpToolException("Err_Mcp_ScopeDenied",
                "The record-level scope does not permit this change.");
        }
    }

    /// <summary>
    /// Gate of the final/destructive sub-operations of the pflege tools (toolkatalog §3.4,
    /// approval-konzept §2.2): the delete/archive/merge scope right is verified first
    /// (approval never replaces the scope check — a missing Löschung right denies
    /// immediately). In <see cref="McpToolExecutionMode.Interactive"/> mode the call then
    /// answers <c>Err_Mcp_ApprovalRequired</c> with a description of the effect (the
    /// approval-queue redirection is wired at dispatcher level with EP-09 Teil B); in
    /// <see cref="McpToolExecutionMode.ApprovedExecution"/> mode — an owner-approved queue
    /// execution — the supplied <paramref name="executeApproved"/> delegate performs the
    /// real service effect instead.
    /// </summary>
    /// <param name="scopeEvaluator">The scope evaluator of the request scope.</param>
    /// <param name="context">The tool call context (carries the execution mode).</param>
    /// <param name="dataType">Data type whose Löschung right is required.</param>
    /// <param name="operation">The requested sub-operation.</param>
    /// <param name="effect">Human-readable description of the destructive effect.</param>
    /// <param name="parameters">Optional parameter snapshot for the queue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="executeApproved">
    /// The real service effect to run under <see cref="McpToolExecutionMode.ApprovedExecution"/>;
    /// <see langword="null"/> while the concrete operation has not been wired to the queue
    /// yet (EP-10 wiring) — execution then fails classified with <c>Err_Mcp_ActionNotExecutable</c>.
    /// </param>
    /// <param name="requiredRight">The right the access must grant for the operation —
    /// <see cref="McpRightKind.Delete"/> by default (delete-equivalent operations); set
    /// <see cref="McpRightKind.Modify"/> for approval-required but non-destructive effects.</param>
    /// <returns>The structured approval-required result, or the execution result.</returns>
    /// <exception cref="McpToolException">
    /// The required scope is not granted, or the operation has no wired execution path yet.
    /// </exception>
    public static async Task<object> ApprovalRequiredAsync(
        IMcpScopeEvaluator scopeEvaluator, McpToolCallContext context,
        McpDataType dataType, string operation, string effect,
        object? parameters, CancellationToken ct,
        Func<CancellationToken, Task<object>>? executeApproved = null,
        McpRightKind requiredRight = McpRightKind.Delete)
    {
        var entity = context.Access.Access;
        if (entity is not null
            && !await scopeEvaluator.IsAllowedAsync(entity, dataType, requiredRight, ct))
        {
            var rightLabel = requiredRight switch
            {
                McpRightKind.Delete => "Löschung",
                McpRightKind.Modify => "Änderung",
                McpRightKind.Create => "Erstellung",
                McpRightKind.Read => "Lesen",
                _ => requiredRight.ToString()
            };
            throw new McpToolException("Err_Mcp_ScopeDenied",
                $"Operation '{operation}' requires the scope ({dataType}, {rightLabel}).");
        }

        if (context.Mode == McpToolExecutionMode.ApprovedExecution)
        {
            // Owner-approved queue execution: run the real effect — or fail classified when
            // the concrete operation is not wired yet (EP-10 wires the delegates).
            if (executeApproved is null)
            {
                throw new McpToolException("Err_Mcp_ActionNotExecutable",
                    $"Operation '{operation}' has no execution path wired yet.");
            }
            return await executeApproved(ct);
        }

        return new
        {
            requiresApproval = true,
            code = "Err_Mcp_ApprovalRequired",
            operation,
            effect,
            parameters,
            message = "This operation is final/destructive and requires owner approval."
        };
    }

    /// <summary>
    /// Execution-time existence gate of a destructive operation (approval-konzept.md §5.2):
    /// a request approved against a record that vanished between approval and queue
    /// execution must end <c>Failed</c> with the classified reason <c>Err_TargetGone</c>
    /// instead of answering the silent 404-shaped result of the interactive path.
    /// </summary>
    /// <param name="context">The tool call context (carries the execution mode).</param>
    /// <param name="target">The resolved target record, or <see langword="null"/>.</param>
    /// <param name="operation">The requested sub-operation (diagnostic).</param>
    /// <returns><see langword="true"/> when the caller should emit its normal
    /// target-missing answer (interactive mode with a missing target).</returns>
    /// <exception cref="McpToolException">
    /// Approved-execution mode with a missing target — <c>Err_TargetGone</c>.
    /// </exception>
    public static bool ThrowIfExecutionTargetGone(McpToolCallContext context, object? target, string operation)
    {
        if (target is null && context.Mode == McpToolExecutionMode.ApprovedExecution)
        {
            throw new McpToolException("Err_TargetGone",
                $"The target of operation '{operation}' no longer exists.");
        }
        return target is null;
    }

    // ------------------------------------------------------------------
    // Foreign-reference resolution (a foreign id behaves like a missing record)
    // ------------------------------------------------------------------

    /// <summary>Resolves a contact category of the owner or rejects the call.</summary>
    /// <param name="categories">Category service contract.</param>
    /// <param name="categoryId">Referenced category id.</param>
    /// <param name="owner">Owner user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved category.</returns>
    /// <exception cref="McpToolException">The reference cannot be resolved for the owner.</exception>
    public static async Task<ContactCategoryDto> RequireContactCategoryAsync(
        IContactCategoryService categories, Guid categoryId, Guid owner, CancellationToken ct)
        => await categories.GetAsync(categoryId, owner, ct)
           ?? throw new McpToolException("Err_Mcp_InvalidArguments",
               "The referenced contact category does not exist or is not owned by the caller.");

    /// <summary>Resolves a savings-plan category of the owner or rejects the call.</summary>
    /// <param name="categories">Category service contract.</param>
    /// <param name="categoryId">Referenced category id.</param>
    /// <param name="owner">Owner user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved category.</returns>
    /// <exception cref="McpToolException">The reference cannot be resolved for the owner.</exception>
    public static async Task<SavingsPlanCategoryDto> RequireSavingsPlanCategoryAsync(
        ISavingsPlanCategoryService categories, Guid categoryId, Guid owner, CancellationToken ct)
        => await categories.GetAsync(categoryId, owner, ct)
           ?? throw new McpToolException("Err_Mcp_InvalidArguments",
               "The referenced savings-plan category does not exist or is not owned by the caller.");

    /// <summary>Resolves a security category of the owner or rejects the call.</summary>
    /// <param name="categories">Category service contract.</param>
    /// <param name="categoryId">Referenced category id.</param>
    /// <param name="owner">Owner user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved category.</returns>
    /// <exception cref="McpToolException">The reference cannot be resolved for the owner.</exception>
    public static async Task<SecurityCategoryDto> RequireSecurityCategoryAsync(
        ISecurityCategoryService categories, Guid categoryId, Guid owner, CancellationToken ct)
        => await categories.GetAsync(categoryId, owner, ct)
           ?? throw new McpToolException("Err_Mcp_InvalidArguments",
               "The referenced security category does not exist or is not owned by the caller.");

    /// <summary>Resolves an attachment category of the owner or rejects the call.</summary>
    /// <param name="categories">Category service contract.</param>
    /// <param name="categoryId">Referenced category id.</param>
    /// <param name="owner">Owner user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved category.</returns>
    /// <exception cref="McpToolException">The reference cannot be resolved for the owner.</exception>
    public static async Task<AttachmentCategoryDto> RequireAttachmentCategoryAsync(
        IAttachmentCategoryService categories, Guid categoryId, Guid owner, CancellationToken ct)
    {
        var list = await categories.ListAsync(owner, ct);
        return list.FirstOrDefault(c => c.Id == categoryId)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                "The referenced attachment category does not exist or is not owned by the caller.");
    }

    /// <summary>Resolves a budget category of the owner or rejects the call.</summary>
    /// <param name="categories">Category service contract.</param>
    /// <param name="categoryId">Referenced category id.</param>
    /// <param name="owner">Owner user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved category.</returns>
    /// <exception cref="McpToolException">The reference cannot be resolved for the owner.</exception>
    public static async Task<BudgetCategoryDto> RequireBudgetCategoryAsync(
        IBudgetCategoryService categories, Guid categoryId, Guid owner, CancellationToken ct)
        => await categories.GetAsync(categoryId, owner, ct)
           ?? throw new McpToolException("Err_Mcp_InvalidArguments",
               "The referenced budget category does not exist or is not owned by the caller.");

    /// <summary>Resolves a budget purpose of the owner or rejects the call.</summary>
    /// <param name="purposes">Purpose service contract.</param>
    /// <param name="purposeId">Referenced purpose id.</param>
    /// <param name="owner">Owner user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved purpose.</returns>
    /// <exception cref="McpToolException">The reference cannot be resolved for the owner.</exception>
    public static async Task<BudgetPurposeDto> RequireBudgetPurposeAsync(
        IBudgetPurposeService purposes, Guid purposeId, Guid owner, CancellationToken ct)
        => await purposes.GetAsync(purposeId, owner, ct)
           ?? throw new McpToolException("Err_Mcp_InvalidArguments",
               "The referenced budget purpose does not exist or is not owned by the caller.");

    /// <summary>
    /// Resolves the owner-scoped target entity of an attachment operation
    /// (upload/url/reference/reassign destination — the "Decke der Ziel-Entität" of
    /// F-0803): only master-data entity kinds resolvable via the regular service
    /// contracts are accepted; ledger-owned kinds (postings, statement entries/imports,
    /// draft entries) are reachable through the booking pipeline, not this tool.
    /// </summary>
    /// <param name="kind">The attachment entity kind.</param>
    /// <param name="entityId">The referenced entity id.</param>
    /// <param name="owner">Owner user id.</param>
    /// <param name="resolve">Per-kind resolver supplied by the calling handler.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="McpToolException">Unsupported kind or unresolvable reference.</exception>
    public static async Task RequireAttachmentTargetAsync(
        AttachmentEntityKind kind, Guid entityId, Guid owner,
        Func<AttachmentEntityKind, Guid, Guid, CancellationToken, Task<bool>> resolve,
        CancellationToken ct)
    {
        if (!await resolve(kind, entityId, owner, ct))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "The referenced attachment target entity does not exist or is not owned by the caller.");
        }
    }

    // ------------------------------------------------------------------
    // Plausibility helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Reads a required, non-empty name argument (trimmed, length-capped).
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <param name="maxLength">Maximum accepted length.</param>
    /// <returns>The trimmed name.</returns>
    /// <exception cref="McpToolException">Missing or oversized value.</exception>
    public static string RequiredName(JsonElement args, string name = "name", int maxLength = MaxNameLength)
    {
        var value = McpToolArguments.OptionalString(args, name, maxLength + 1);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{name}' is required.");
        }
        if (value.Trim().Length > maxLength)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Argument '{name}' must not exceed {maxLength} characters.");
        }
        return value.Trim();
    }

    /// <summary>
    /// Reads an optional decimal argument.
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <returns>The parsed value or <see langword="null"/>.</returns>
    /// <exception cref="McpToolException">The value is present but not a number.</exception>
    public static decimal? OptionalDecimal(JsonElement args, string name)
    {
        if (!McpToolArguments.HasNonNull(args, name))
        {
            return null;
        }
        var el = args.GetProperty(name);
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var value))
        {
            return value;
        }
        throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{name}' must be a number.");
    }

    /// <summary>
    /// Reads an optional <see cref="DateOnly"/> argument (ISO-8601 date).
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <returns>The parsed value or <see langword="null"/>.</returns>
    /// <exception cref="McpToolException">The value is present but unparseable.</exception>
    public static DateOnly? OptionalDateOnly(JsonElement args, string name)
    {
        if (!McpToolArguments.HasNonNull(args, name))
        {
            return null;
        }
        var el = args.GetProperty(name);
        if (el.ValueKind == JsonValueKind.String
            && DateOnly.TryParse(el.GetString(), CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }
        throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{name}' must be an ISO-8601 date string.");
    }

    /// <summary>
    /// Validates an IBAN-shaped value plausibly (alphanumeric, length-capped); the
    /// uniqueness check stays with the service.
    /// </summary>
    /// <param name="iban">The IBAN value.</param>
    /// <param name="argument">The argument name for the error message.</param>
    /// <returns>The normalized IBAN.</returns>
    /// <exception cref="McpToolException">Malformed value.</exception>
    public static string RequireIban(string? iban, string argument = "iban")
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{argument}' is required.");
        }
        var normalized = iban.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        if (normalized.Length > MaxIbanLength
            || !normalized.All(c => char.IsLetterOrDigit(c)))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Argument '{argument}' must be an alphanumeric IBAN of at most {MaxIbanLength} characters.");
        }
        return normalized;
    }

    /// <summary>
    /// Validates an absolute http(s) URL for URL attachments.
    /// </summary>
    /// <param name="url">The URL value.</param>
    /// <returns>The URL.</returns>
    /// <exception cref="McpToolException">Missing, malformed or non-http(s) URL.</exception>
    public static string RequireHttpUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'url' is required.");
        }
        if (url.Length > MaxUrlLength
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'url' must be an absolute http(s) URL of at most " + MaxUrlLength + " characters.");
        }
        return url;
    }

    /// <summary>
    /// Decodes a base64 upload payload with the MCP size cap (the JSON-RPC envelope
    /// already limits transport size; this bound keeps memory bounded early).
    /// </summary>
    /// <param name="contentBase64">The base64 argument value.</param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="McpToolException">Missing, malformed or oversized payload.</exception>
    public static byte[] DecodeUpload(string? contentBase64)
    {
        if (string.IsNullOrWhiteSpace(contentBase64))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'contentBase64' is required.");
        }
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'contentBase64' is not valid base64.");
        }
        if (bytes.Length == 0)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'contentBase64' must not be empty.");
        }
        if (bytes.Length > MaxUploadBytes)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                $"The upload exceeds the MCP size limit of {MaxUploadBytes} bytes — use the API upload for larger files.");
        }
        return bytes;
    }

    // ------------------------------------------------------------------
    // Audit field-delta helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Appends one field delta when the before/after values differ (invariant-culture
    /// serialization; <see cref="DateTime"/>/<see cref="DateOnly"/> round-trip formatted).
    /// </summary>
    /// <param name="changes">The change list.</param>
    /// <param name="entityType">Affected entity type.</param>
    /// <param name="entityId">Affected record id.</param>
    /// <param name="field">Changed field name.</param>
    /// <param name="oldValue">Value before.</param>
    /// <param name="newValue">Value after.</param>
    public static void AddChange(List<McpAuditFieldChange> changes, string entityType, Guid entityId,
        string field, object? oldValue, object? newValue)
    {
        if (Equals(oldValue, newValue))
        {
            return;
        }
        changes.Add(new McpAuditFieldChange(entityType, entityId, field,
            SerializeValue(oldValue), SerializeValue(newValue)));
    }

    /// <summary>
    /// Always appends a field delta (for create/add markers whose before value is
    /// systematically <see langword="null"/>).
    /// </summary>
    /// <param name="changes">The change list.</param>
    /// <param name="entityType">Affected entity type.</param>
    /// <param name="entityId">Affected record id.</param>
    /// <param name="field">Changed field name.</param>
    /// <param name="oldValue">Value before.</param>
    /// <param name="newValue">Value after.</param>
    public static void AddChangeAlways(List<McpAuditFieldChange> changes, string entityType, Guid entityId,
        string field, object? oldValue, object? newValue)
        => changes.Add(new McpAuditFieldChange(entityType, entityId, field,
            SerializeValue(oldValue), SerializeValue(newValue)));

    private static string? SerializeValue(object? value)
        => value switch
        {
            null => null,
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("O", CultureInfo.InvariantCulture),
            decimal dec => dec.ToString(CultureInfo.InvariantCulture),
            double dbl => dbl.ToString(CultureInfo.InvariantCulture),
            float flt => flt.ToString(CultureInfo.InvariantCulture),
            Enum e => e.ToString(),
            _ => value.ToString()
        };
}
