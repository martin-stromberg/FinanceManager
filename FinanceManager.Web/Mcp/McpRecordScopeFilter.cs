using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Record-level (datensatzscharf) read filter for MCP tool adapters (stages 1–2 of the
/// permission model, berechtigungsmodell.md §4/§6). Ownership (stage 0) is already enforced
/// by the <c>ownerUserId</c>-scoping of every called <c>I*Service</c> contract and the scope
/// tuple (stage 1) is checked by the dispatcher — this helper applies the remaining
/// per-record scope filters (account types and the four category dimensions) with the same
/// semantics as <c>McpScopeEvaluator</c>: OR over matching scope rows, AND over the five
/// dimensions, a set CSV acts as a positive whitelist and never matches unclassified
/// records.
/// </summary>
/// <remarks>
/// The effective profile is resolved once per request scope and cached — standard access
/// levels carry filter-less synthetic rows, so the common path costs a single profile
/// resolution and no per-record database work. Filtered rows only arrive with individual
/// profiles (EP-05); adapters then supply the classification values of each record via
/// <see cref="McpRecordContext"/> and must resolve "filter through aggregates" (e.g. the
/// account type of a bank posting or the category of a linked security) through the regular
/// service contracts.
/// </remarks>
public sealed class McpRecordScopeFilter
{
    private readonly IMcpScopeEvaluator _evaluator;
    private readonly Dictionary<(Guid AccessId, McpDataType DataType, McpRightKind Right), IReadOnlyList<McpAccessScope>> _cache = new();

    /// <summary>Creates the filter.</summary>
    /// <param name="evaluator">The scope evaluator resolving the effective profile.</param>
    public McpRecordScopeFilter(IMcpScopeEvaluator evaluator)
    {
        _evaluator = evaluator;
    }

    /// <summary>
    /// Resolves the scope rows of the effective profile matching
    /// (<paramref name="dataType"/>, <see cref="McpRightKind.Read"/>). Cached per request.
    /// </summary>
    /// <param name="access">The MCP access context.</param>
    /// <param name="dataType">The data type of the records being emitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching scope rows (empty when the tuple is not granted).</returns>
    public Task<IReadOnlyList<McpAccessScope>> GetReadScopesAsync(
        IMcpAccessContext access, McpDataType dataType, CancellationToken ct)
        => GetScopesAsync(access, dataType, McpRightKind.Read, ct);

    /// <summary>
    /// Resolves the scope rows of the effective profile matching
    /// (<paramref name="dataType"/>, <paramref name="right"/>). Cached per request.
    /// Suggestion tools (EP-04) evaluate record filters against their
    /// <see cref="McpRightKind.Suggest"/> rows instead of the read rows.
    /// </summary>
    /// <param name="access">The MCP access context.</param>
    /// <param name="dataType">The data type of the records being emitted.</param>
    /// <param name="right">The exercised right kind.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching scope rows (empty when the tuple is not granted).</returns>
    public async Task<IReadOnlyList<McpAccessScope>> GetScopesAsync(
        IMcpAccessContext access, McpDataType dataType, McpRightKind right, CancellationToken ct)
    {
        var entity = access.Access;
        if (entity is null)
        {
            return Array.Empty<McpAccessScope>();
        }
        var key = (entity.Id, dataType, right);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var profile = await _evaluator.GetEffectiveScopesAsync(entity, ct);
        var result = (IReadOnlyList<McpAccessScope>)profile
            .Where(s => s.DataType == dataType && s.Right == right)
            .ToList();
        _cache[key] = result;
        return result;
    }

    /// <summary>
    /// Whether any of the matching scope rows carries a filter CSV — i.e. whether
    /// per-record evaluation is required at all.
    /// </summary>
    /// <param name="scopes">Rows returned by <see cref="GetReadScopesAsync"/>.</param>
    /// <returns><see langword="true"/> when at least one row restricts records.</returns>
    public static bool RequiresRecordFilter(IReadOnlyList<McpAccessScope> scopes)
        => scopes.Any(HasFilter);

    /// <summary>Whether one scope row carries any filter CSV.</summary>
    /// <param name="scope">The scope row.</param>
    /// <returns><see langword="true"/> when the row restricts records.</returns>
    public static bool HasFilter(McpAccessScope scope)
        => !string.IsNullOrWhiteSpace(scope.AccountTypesCsv)
           || !string.IsNullOrWhiteSpace(scope.ContactCategoryIdsCsv)
           || !string.IsNullOrWhiteSpace(scope.SecurityCategoryIdsCsv)
           || !string.IsNullOrWhiteSpace(scope.SavingsPlanCategoryIdsCsv)
           || !string.IsNullOrWhiteSpace(scope.AttachmentCategoryIdsCsv);

    /// <summary>
    /// Evaluates the stage-2 filter verdict for one record: visible when at least one
    /// matching scope row accepts the record's classification values.
    /// </summary>
    /// <param name="scopes">Rows returned by <see cref="GetReadScopesAsync"/>.</param>
    /// <param name="record">Classification values of the record.</param>
    /// <returns><see langword="true"/> when the record may be emitted.</returns>
    public static bool IsAllowed(IReadOnlyList<McpAccessScope> scopes, McpRecordContext record)
        => scopes.Any(s => FiltersMatch(s, record));

    /// <summary>
    /// Filters a materialized result list. When no matching row carries filters the list is
    /// returned unchanged (no per-record work).
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="access">The MCP access context.</param>
    /// <param name="dataType">The data type of the records.</param>
    /// <param name="items">The service-provided, already owner-scoped items.</param>
    /// <param name="recordContext">Maps an item to its filter classification values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The visible subset.</returns>
    public async Task<IReadOnlyList<T>> ApplyAsync<T>(
        IMcpAccessContext access,
        McpDataType dataType,
        IReadOnlyList<T> items,
        Func<T, McpRecordContext> recordContext,
        CancellationToken ct)
    {
        var scopes = await GetReadScopesAsync(access, dataType, ct);
        if (!RequiresRecordFilter(scopes))
        {
            return items;
        }
        return items.Where(i => IsAllowed(scopes, recordContext(i))).ToList();
    }

    /// <summary>
    /// Evaluates the record-level filter verdict for a single record.
    /// </summary>
    /// <param name="access">The MCP access context.</param>
    /// <param name="dataType">The data type of the record.</param>
    /// <param name="record">Classification values of the record.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the record may be emitted.</returns>
    public async Task<bool> IsVisibleAsync(
        IMcpAccessContext access, McpDataType dataType, McpRecordContext record, CancellationToken ct)
    {
        var scopes = await GetReadScopesAsync(access, dataType, ct);
        if (!RequiresRecordFilter(scopes))
        {
            return true;
        }
        return IsAllowed(scopes, record);
    }

    private static bool FiltersMatch(McpAccessScope scope, McpRecordContext record)
        => CsvMatchesShort(scope.AccountTypesCsv, record.AccountTypeValue)
           && CsvMatchesGuid(scope.ContactCategoryIdsCsv, record.ContactCategoryId)
           && CsvMatchesGuid(scope.SecurityCategoryIdsCsv, record.SecurityCategoryId)
           && CsvMatchesGuid(scope.SavingsPlanCategoryIdsCsv, record.SavingsPlanCategoryId)
           && CsvMatchesGuid(scope.AttachmentCategoryIdsCsv, record.AttachmentCategoryId);

    private static bool CsvMatchesShort(string? csv, short? value)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return true;
        }
        if (!value.HasValue)
        {
            return false;
        }
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (short.TryParse(part, out var parsed) && parsed == value.Value)
            {
                return true;
            }
        }
        return false;
    }

    private static bool CsvMatchesGuid(string? csv, Guid? value)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return true;
        }
        if (!value.HasValue)
        {
            return false;
        }
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(part, out var parsed) && parsed == value.Value)
            {
                return true;
            }
        }
        return false;
    }
}
