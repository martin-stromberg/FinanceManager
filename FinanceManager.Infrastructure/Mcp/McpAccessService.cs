using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Administration service of MCP accesses (F-0104). Creates accesses with a one-time
/// plaintext token, rotates credentials with immediate invalidation, revokes, activates /
/// deactivates and manages individual scope profiles with F3-matrix validation. All queries
/// are strictly owner-scoped; foreign ids behave like non-existing records.
/// </summary>
public sealed class McpAccessService : IMcpAccessService
{
    /// <summary>Paging clamp consistent with the API convention (skip/take ≤ 200).</summary>
    private const int MaxTake = 200;
    private const int HintCollisionRetries = 3;

    private readonly AppDbContext _db;
    private readonly IMcpTokenIssuer _tokenIssuer;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly IMcpApprovalService _approvalService;
    private readonly ILogger<McpAccessService> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="McpAccessService"/>.
    /// </summary>
    /// <param name="db">Application database context.</param>
    /// <param name="tokenIssuer">Token issuer for creation and rotation.</param>
    /// <param name="scopeEvaluator">Evaluator used to render effective profiles.</param>
    /// <param name="auditWriter">Hash-chained audit write path (F-0301/F-0302); audit bundles are staged
    /// and commit atomically with the access change in one <c>SaveChangesAsync</c>.</param>
    /// <param name="approvalService">Approval-request queue: deactivation/revocation withdraws the
    /// still-open requests of the access (approval-konzept.md §6.3; the executor's access re-check
    /// remains the safety net).</param>
    /// <param name="logger">Logger.</param>
    public McpAccessService(
        AppDbContext db,
        IMcpTokenIssuer tokenIssuer,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        IMcpApprovalService approvalService,
        ILogger<McpAccessService> logger)
    {
        _db = db;
        _tokenIssuer = tokenIssuer;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _approvalService = approvalService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<McpAccessListItemDto>> ListAsync(Guid ownerUserId, string? search, int skip, int take, CancellationToken ct)
    {
        var utcNow = DateTime.UtcNow;
        var query = _db.McpAccesses.AsNoTracking().Where(a => a.OwnerUserId == ownerUserId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a => a.Name.Contains(s));
        }
        var list = await query
            .OrderBy(a => a.Name)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, MaxTake))
            .ToListAsync(ct);
        return list.Select(a => ToListItem(a, utcNow)).ToList();
    }

    /// <inheritdoc />
    public async Task<McpAccessDetailDto?> GetAsync(Guid ownerUserId, Guid id, CancellationToken ct)
    {
        var access = await _db.McpAccesses.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.OwnerUserId == ownerUserId, ct);
        if (access is null)
        {
            return null;
        }
        var scopes = await _scopeEvaluator.GetEffectiveScopesAsync(access, ct);
        return ToDetail(access, scopes, DateTime.UtcNow);
    }

    /// <inheritdoc />
    public async Task<McpAccessCreatedDto> CreateAsync(Guid ownerUserId, McpAccessCreateRequest request, CancellationToken ct)
    {
        var level = CheckedEnum<McpAccessLevel>(request.Level, nameof(request.Level));
        var approvalMode = CheckedEnum<McpApprovalMode>(request.ApprovalMode, nameof(request.ApprovalMode));
        var scopeDtos = request.Scopes ?? Array.Empty<McpAccessScopeDto>();
        if (level != McpAccessLevel.Individuell && scopeDtos.Count > 0)
        {
            throw new DomainValidationException("Err_Mcp_InvalidScope", "Persisted scopes are only allowed for the individual access level.");
        }
        ValidateScopes(scopeDtos);

        var name = (request.Name ?? string.Empty).Trim();
        if (await _db.McpAccesses.AnyAsync(a => a.OwnerUserId == ownerUserId && a.Name == name, ct))
        {
            throw new DomainValidationException("Err_Mcp_NameInUse", "An access with this name already exists.");
        }

        for (var attempt = 0; ; attempt++)
        {
            var issued = _tokenIssuer.Issue();
            var access = new McpAccess(ownerUserId, name, issued.AccessKeyHint, issued.TokenHash, level, approvalMode,
                request.Description, request.ExpiresUtc, request.AllowedIpRanges);
            if (level == McpAccessLevel.Individuell)
            {
                access.ReplaceIndividualScopes(scopeDtos.Select(s => ToScope(access, s)));
            }
            _db.McpAccesses.Add(access);
            // Transactional coupling: the audit record commits in the same SaveChanges
            // round as the new access — an effect without its record fails (F-0301).
            var staged = await StageAuditAsync(access, "AccessCreated", ct);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (attempt < HintCollisionRetries)
            {
                _logger.LogWarning(ex, "MCP access insert failed on attempt {Attempt}; retrying with a fresh token", attempt + 1);
                _db.Entry(access).State = EntityState.Detached;
                if (staged is not null)
                {
                    _db.Entry(staged).State = EntityState.Detached;
                }
                continue; // access-key-hint collision: issue a fresh token
            }
            var scopes = await _scopeEvaluator.GetEffectiveScopesAsync(access, ct);
            return new McpAccessCreatedDto(ToDetail(access, scopes, DateTime.UtcNow), issued.PlainText);
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Guid ownerUserId, Guid id, McpAccessUpdateRequest request, CancellationToken ct)
    {
        var access = await _db.McpAccesses.Include(a => a.Scopes)
            .FirstOrDefaultAsync(a => a.Id == id && a.OwnerUserId == ownerUserId, ct);
        if (access is null)
        {
            return false;
        }
        var level = CheckedEnum<McpAccessLevel>(request.Level, nameof(request.Level));
        var approvalMode = CheckedEnum<McpApprovalMode>(request.ApprovalMode, nameof(request.ApprovalMode));
        var name = (request.Name ?? string.Empty).Trim();
        if (!string.Equals(access.Name, name, StringComparison.Ordinal)
            && await _db.McpAccesses.AnyAsync(a => a.OwnerUserId == ownerUserId && a.Name == name && a.Id != id, ct))
        {
            throw new DomainValidationException("Err_Mcp_NameInUse", "An access with this name already exists.");
        }
        // audit-konzept.md §3.3: configuration changes of the access carry a before/after
        // delta — capture the auditable state before the mutation.
        var before = AccessConfigSnapshot.Capture(access);
        access.Update(name, request.Description, level, approvalMode, request.ExpiresUtc, request.AllowedIpRanges);
        await StageAuditAsync(access, "AccessUpdated", ct, BuildAccessDelta(before, access));
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SetScopesAsync(Guid ownerUserId, Guid id, McpScopeSetRequest request, CancellationToken ct)
    {
        var access = await _db.McpAccesses.Include(a => a.Scopes)
            .FirstOrDefaultAsync(a => a.Id == id && a.OwnerUserId == ownerUserId, ct);
        if (access is null)
        {
            return false;
        }
        if (access.Level != McpAccessLevel.Individuell)
        {
            throw new DomainValidationException("Err_Mcp_InvalidScope", "Persisted scopes are only allowed for the individual access level.");
        }
        var scopeDtos = request.Scopes ?? Array.Empty<McpAccessScopeDto>();
        ValidateScopes(scopeDtos);
        // Explicitly track the new rows: entities added only via a navigation collection are
        // treated as existing (pre-assigned keys) and would fail on update.
        var scopesBefore = SnapshotScopes(access.Scopes);
        var newScopes = scopeDtos.Select(s => ToScope(access, s)).ToList();
        await _db.McpAccessScopes.AddRangeAsync(newScopes, ct);
        access.ReplaceIndividualScopes(newScopes);
        // audit-konzept.md §3.3: scope changes are recorded as a before/after scope delta.
        await StageAuditAsync(access, "ScopesChanged", ct,
            BuildScopeDelta(scopesBefore, SnapshotScopes(access.Scopes), access.Id));
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SetFiltersAsync(Guid ownerUserId, Guid id, Guid scopeId, McpFilterSetRequest request, CancellationToken ct)
    {
        var scope = await _db.McpAccessScopes
            .FirstOrDefaultAsync(s => s.Id == scopeId && s.McpAccessId == id && s.OwnerUserId == ownerUserId, ct);
        if (scope is null)
        {
            return false;
        }
        var filtersBefore = new ScopeFilterSnapshot(scope);
        scope.SetFilters(request.AccountTypesCsv, request.ContactCategoryIdsCsv,
            request.SecurityCategoryIdsCsv, request.SavingsPlanCategoryIdsCsv, request.AttachmentCategoryIdsCsv);
        var access = await _db.McpAccesses.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (access is null)
        {
            // The scope references the access — a missing access row is inconsistent and
            // must not persist a filter change without its audit record.
            return false;
        }
        // audit-konzept.md §3.3: filter changes are recorded per dimension as before/after.
        await StageAuditAsync(access, "FiltersChanged", ct, BuildFilterDelta(filtersBefore, scope));
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public Task<bool> ActivateAsync(Guid ownerUserId, Guid id, CancellationToken ct)
        => ChangeStatusAsync(ownerUserId, id, a => a.Activate(), "AccessActivated", ct, withdrawReason: null);

    /// <inheritdoc />
    public Task<bool> DeactivateAsync(Guid ownerUserId, Guid id, CancellationToken ct)
        => ChangeStatusAsync(ownerUserId, id, a => a.Deactivate(), "AccessDeactivated", ct,
            withdrawReason: "Zugang deaktiviert");

    private async Task<bool> ChangeStatusAsync(Guid ownerUserId, Guid id, Action<McpAccess> change, string eventType,
        CancellationToken ct, string? withdrawReason)
    {
        var access = await _db.McpAccesses.FirstOrDefaultAsync(a => a.Id == id && a.OwnerUserId == ownerUserId, ct);
        if (access is null)
        {
            return false;
        }
        try
        {
            change(access);
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainValidationException("Err_Mcp_InvalidState", ex.Message);
        }
        await StageAuditAsync(access, eventType, ct);
        await _db.SaveChangesAsync(ct);
        if (withdrawReason is not null)
        {
            // approval-konzept.md §6.3: the status change must also stop banked effects —
            // open (pending / approved-not-yet-started) requests of the access are withdrawn
            // with a documented system reason, each transition coupling its audit record.
            await _approvalService.WithdrawOpenForAccessAsync(access.Id, withdrawReason, ct);
        }
        return true;
    }

    /// <inheritdoc />
    public async Task<McpAccessRotatedDto?> RotateTokenAsync(Guid ownerUserId, Guid id, CancellationToken ct)
    {
        var access = await _db.McpAccesses.FirstOrDefaultAsync(a => a.Id == id && a.OwnerUserId == ownerUserId, ct);
        if (access is null)
        {
            return null;
        }
        for (var attempt = 0; ; attempt++)
        {
            var issued = _tokenIssuer.Issue();
            access.RotateToken(issued.AccessKeyHint, issued.TokenHash);
            // Transactional coupling: rotation and audit record commit in one save round.
            var staged = await StageAuditAsync(access, "TokenRotated", ct);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (attempt < HintCollisionRetries)
            {
                _logger.LogWarning(ex, "MCP token rotation save failed on attempt {Attempt}; retrying with a fresh token", attempt + 1);
                if (staged is not null)
                {
                    _db.Entry(staged).State = EntityState.Detached;
                }
                continue;
            }
            return new McpAccessRotatedDto(access.Id, issued.PlainText, access.TokenIssuedUtc);
        }
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(Guid ownerUserId, Guid id, string? reason, CancellationToken ct)
    {
        var access = await _db.McpAccesses.FirstOrDefaultAsync(a => a.Id == id && a.OwnerUserId == ownerUserId, ct);
        if (access is null)
        {
            return false;
        }
        access.Revoke(string.IsNullOrWhiteSpace(reason) ? "Revoked by owner" : reason.Trim());
        await StageAuditAsync(access, "AccessRevoked", ct);
        await _db.SaveChangesAsync(ct);
        // approval-konzept.md §6.3: revocation withdraws all still-open requests of the
        // access (system transition before execution start, documented reason).
        await _approvalService.WithdrawOpenForAccessAsync(access.Id, "Zugang entzogen", ct);
        return true;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<McpAccessLevelDto>> GetLevelsAsync(CancellationToken ct)
    {
        var levels = Enum.GetValues<McpAccessLevel>()
            .Select(level => new McpAccessLevelDto(
                (short)level,
                level.ToString(),
                McpLevelProfiles.GetDefaultScopes(level)
                    .Select(t => new McpAccessScopeDto(Guid.Empty, (short)t.DataType, (short)t.Right, null, null, null, null, null))
                    .ToList()))
            .ToList();
        return Task.FromResult<IReadOnlyList<McpAccessLevelDto>>(levels);
    }

    /// <inheritdoc />
    public async Task<McpAccessOptionsDto> GetOptionsAsync(Guid ownerUserId, bool isTlsRequest, CancellationToken ct)
    {
        var dataTypes = Enum.GetValues<McpDataType>()
            .Select(t => new McpDataTypeOptionDto(
                (short)t,
                t.ToString(),
                McpScopeMatrix.RightsFor(t).Select(r => (short)r).ToList()))
            .ToList();
        var rightKinds = Enum.GetValues<McpRightKind>().Select(r => (short)r).ToList();
        var filterDimensions = new[]
        {
            "AccountTypes",
            "ContactCategories",
            "SecurityCategories",
            "SavingsPlanCategories",
            "AttachmentCategories"
        };

        var contactCategories = await _db.ContactCategories.AsNoTracking()
            .Where(c => c.OwnerUserId == ownerUserId).OrderBy(c => c.Name)
            .Select(c => new McpCategoryOptionDto(c.Id, c.Name)).ToListAsync(ct);
        var securityCategories = await _db.SecurityCategories.AsNoTracking()
            .Where(c => c.OwnerUserId == ownerUserId).OrderBy(c => c.Name)
            .Select(c => new McpCategoryOptionDto(c.Id, c.Name)).ToListAsync(ct);
        var savingsPlanCategories = await _db.SavingsPlanCategories.AsNoTracking()
            .Where(c => c.OwnerUserId == ownerUserId).OrderBy(c => c.Name)
            .Select(c => new McpCategoryOptionDto(c.Id, c.Name)).ToListAsync(ct);
        var attachmentCategories = await _db.AttachmentCategories.AsNoTracking()
            .Where(c => c.OwnerUserId == ownerUserId).OrderBy(c => c.Name)
            .Select(c => new McpCategoryOptionDto(c.Id, c.Name)).ToListAsync(ct);

        return new McpAccessOptionsDto(
            dataTypes,
            rightKinds,
            filterDimensions,
            contactCategories,
            securityCategories,
            savingsPlanCategories,
            attachmentCategories,
            isTlsRequest);
    }

    /// <summary>
    /// Stages the access event audit record in the shared unit of work. It commits
    /// atomically with the caller's subsequent <c>SaveChangesAsync</c> — a write effect
    /// without its audit record fails, and vice versa (F-0301). Unlike a read-path audit
    /// failure, a staging failure propagates deliberately.
    /// </summary>
    private Task<McpAuditEntry?> StageAuditAsync(McpAccess access, string eventType, CancellationToken ct,
        IReadOnlyList<McpAuditFieldChange>? changes = null)
        => _auditWriter.StageAsync(new McpAuditWriteRequest(
            Category: McpAuditCategory.AccessEvent,
            EventType: eventType,
            ResultStatus: "Success",
            OwnerUserId: access.OwnerUserId,
            McpAccessId: access.Id,
            McpAccessName: access.Name,
            Context: "Ui",
            Changes: changes), ct);

    /// <summary>
    /// Snapshot of the auditable configuration of an access (audit-konzept.md §3.3). Never
    /// carries credential material — <c>TokenHash</c>/<c>AccessKeyHint</c> are deliberately
    /// not part of the snapshot.
    /// </summary>
    private sealed record AccessConfigSnapshot(
        string Name,
        string? Description,
        McpAccessLevel Level,
        McpApprovalMode ApprovalMode,
        DateTime? ExpiresUtc,
        string? AllowedIpRanges,
        IReadOnlyDictionary<string, string> Scopes)
    {
        /// <summary>Captures the current auditable state of <paramref name="access"/>.</summary>
        public static AccessConfigSnapshot Capture(McpAccess access)
            => new(access.Name, access.Description, access.Level, access.ApprovalMode,
                access.ExpiresUtc, access.AllowedIpRanges, SnapshotScopes(access.Scopes));
    }

    /// <summary>The five filter CSVs of one scope row (before-image for filter deltas).</summary>
    private sealed record ScopeFilterSnapshot(
        string? AccountTypesCsv,
        string? ContactCategoryIdsCsv,
        string? SecurityCategoryIdsCsv,
        string? SavingsPlanCategoryIdsCsv,
        string? AttachmentCategoryIdsCsv)
    {
        /// <summary>Captures the current filter CSVs of <paramref name="scope"/>.</summary>
        public ScopeFilterSnapshot(McpAccessScope scope)
            : this(scope.AccountTypesCsv, scope.ContactCategoryIdsCsv, scope.SecurityCategoryIdsCsv,
                scope.SavingsPlanCategoryIdsCsv, scope.AttachmentCategoryIdsCsv)
        {
        }
    }

    /// <summary>
    /// Field delta of the editable access configuration plus the persisted scope profile
    /// (a level switch away from <see cref="McpAccessLevel.Individuell"/> clears the scope
    /// rows — the removal is part of the delta). Only actually changed fields produce rows.
    /// </summary>
    private static List<McpAuditFieldChange> BuildAccessDelta(AccessConfigSnapshot before, McpAccess after)
    {
        var changes = new List<McpAuditFieldChange>();
        AddChange(changes, nameof(McpAccess), after.Id, nameof(McpAccess.Name), before.Name, after.Name);
        AddChange(changes, nameof(McpAccess), after.Id, nameof(McpAccess.Description), before.Description, after.Description);
        AddChange(changes, nameof(McpAccess), after.Id, nameof(McpAccess.Level), before.Level.ToString(), after.Level.ToString());
        AddChange(changes, nameof(McpAccess), after.Id, nameof(McpAccess.ApprovalMode), before.ApprovalMode.ToString(), after.ApprovalMode.ToString());
        AddChange(changes, nameof(McpAccess), after.Id, nameof(McpAccess.ExpiresUtc), FormatUtc(before.ExpiresUtc), FormatUtc(after.ExpiresUtc));
        AddChange(changes, nameof(McpAccess), after.Id, nameof(McpAccess.AllowedIpRanges), before.AllowedIpRanges, after.AllowedIpRanges);
        changes.AddRange(BuildScopeDelta(before.Scopes, SnapshotScopes(after.Scopes), after.Id));
        return changes;
    }

    /// <summary>
    /// Per-dimension filter delta of one scope row: one row per changed CSV field
    /// (<c>null</c> = unrestricted). Only changed dimensions produce rows.
    /// </summary>
    private static List<McpAuditFieldChange> BuildFilterDelta(ScopeFilterSnapshot before, McpAccessScope after)
    {
        var changes = new List<McpAuditFieldChange>(5);
        AddChange(changes, nameof(McpAccessScope), after.Id, nameof(McpAccessScope.AccountTypesCsv), before.AccountTypesCsv, after.AccountTypesCsv);
        AddChange(changes, nameof(McpAccessScope), after.Id, nameof(McpAccessScope.ContactCategoryIdsCsv), before.ContactCategoryIdsCsv, after.ContactCategoryIdsCsv);
        AddChange(changes, nameof(McpAccessScope), after.Id, nameof(McpAccessScope.SecurityCategoryIdsCsv), before.SecurityCategoryIdsCsv, after.SecurityCategoryIdsCsv);
        AddChange(changes, nameof(McpAccessScope), after.Id, nameof(McpAccessScope.SavingsPlanCategoryIdsCsv), before.SavingsPlanCategoryIdsCsv, after.SavingsPlanCategoryIdsCsv);
        AddChange(changes, nameof(McpAccessScope), after.Id, nameof(McpAccessScope.AttachmentCategoryIdsCsv), before.AttachmentCategoryIdsCsv, after.AttachmentCategoryIdsCsv);
        return changes;
    }

    /// <summary>
    /// Scope delta keyed by the <c>DataType:Right</c> tuple: added tuples carry the rendered
    /// after-image (old = <see langword="null"/>), removed tuples the before-image
    /// (new = <see langword="null"/>), tuples with changed filter sets an old→new row.
    /// </summary>
    private static List<McpAuditFieldChange> BuildScopeDelta(
        IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after, Guid accessId)
    {
        var changes = new List<McpAuditFieldChange>();
        foreach (var key in before.Keys.Union(after.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            before.TryGetValue(key, out var oldRows);
            after.TryGetValue(key, out var newRows);
            AddChange(changes, nameof(McpAccessScope), accessId, $"Scope[{key}]", oldRows, newRows);
        }
        return changes;
    }

    /// <summary>
    /// Renders the persisted scope profile as tuple key (<c>DataType:Right</c>) → sorted,
    /// <c>|</c>-joined rendered rows of that tuple (several rows per tuple combine with OR,
    /// berechtigungsmodell.md §4.2).
    /// </summary>
    private static IReadOnlyDictionary<string, string> SnapshotScopes(IEnumerable<McpAccessScope> scopes)
    {
        var groups = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var scope in scopes)
        {
            var key = $"{scope.DataType}:{scope.Right}";
            if (!groups.TryGetValue(key, out var rows))
            {
                groups[key] = rows = new SortedSet<string>(StringComparer.Ordinal);
            }
            rows.Add(RenderScope(scope));
        }
        return groups.ToDictionary(g => g.Key, g => string.Join("|", g.Value), StringComparer.Ordinal);
    }

    /// <summary>
    /// Compact rendering of one scope row: <c>DataType:Right</c> plus the set filter CSVs
    /// (<c>[Dim=csv;…]</c>); an unrestricted row renders as the bare tuple.
    /// </summary>
    private static string RenderScope(McpAccessScope scope)
    {
        var filters = new List<string>(5);
        AddFilter(filters, "AccountTypes", scope.AccountTypesCsv);
        AddFilter(filters, "ContactCategories", scope.ContactCategoryIdsCsv);
        AddFilter(filters, "SecurityCategories", scope.SecurityCategoryIdsCsv);
        AddFilter(filters, "SavingsPlanCategories", scope.SavingsPlanCategoryIdsCsv);
        AddFilter(filters, "AttachmentCategories", scope.AttachmentCategoryIdsCsv);
        return $"{scope.DataType}:{scope.Right}"
            + (filters.Count == 0 ? string.Empty : "[" + string.Join(";", filters) + "]");
    }

    private static void AddFilter(List<string> filters, string dimension, string? csv)
    {
        if (!string.IsNullOrWhiteSpace(csv))
        {
            filters.Add($"{dimension}={csv}");
        }
    }

    private static void AddChange(List<McpAuditFieldChange> changes, string entityType, Guid entityId,
        string field, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            changes.Add(new McpAuditFieldChange(entityType, entityId, field, oldValue, newValue));
        }
    }

    private static string? FormatUtc(DateTime? utc)
        => utc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    /// <summary>
    /// Validates a persisted scope profile: every tuple must be a valid F3-matrix
    /// combination (berechtigungsmodell.md — an individual profile can only ever restrict,
    /// never extend beyond the catalog's normative edges), and every non-<see cref="McpRightKind.Read"/>
    /// right requires a Read row on the same data type within the same profile — the
    /// implication rule of ui-konzept.md §4.3.2 ("Änderung ohne Lesen ist fachlich unsinnig").
    /// </summary>
    private static void ValidateScopes(IReadOnlyList<McpAccessScopeDto> scopes)
    {
        var readableTypes = new HashSet<McpDataType>();
        var rows = new List<(McpDataType DataType, McpRightKind Right)>(scopes.Count);
        foreach (var scope in scopes)
        {
            var dataType = CheckedEnum<McpDataType>(scope.DataType, nameof(scope.DataType));
            var right = CheckedEnum<McpRightKind>(scope.Right, nameof(scope.Right));
            if (!McpScopeMatrix.IsValidCombination(dataType, right))
            {
                throw new DomainValidationException("Err_Mcp_InvalidScope",
                    $"The scope ({dataType}, {right}) is not a valid F3-matrix combination.");
            }
            rows.Add((dataType, right));
            if (right == McpRightKind.Read)
            {
                readableTypes.Add(dataType);
            }
        }
        foreach (var (dataType, right) in rows)
        {
            if (right != McpRightKind.Read && !readableTypes.Contains(dataType))
            {
                throw new DomainValidationException("Err_Mcp_InvalidScope",
                    $"The scope ({dataType}, {right}) requires a Read scope on {dataType}.");
            }
        }
    }

    private static McpAccessScope ToScope(McpAccess access, McpAccessScopeDto dto)
        => new(access.Id, access.OwnerUserId, (McpDataType)dto.DataType, (McpRightKind)dto.Right,
            dto.AccountTypesCsv, dto.ContactCategoryIdsCsv, dto.SecurityCategoryIdsCsv,
            dto.SavingsPlanCategoryIdsCsv, dto.AttachmentCategoryIdsCsv);

    private static TEnum CheckedEnum<TEnum>(short value, string paramName) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(typeof(TEnum), (int)value))
        {
            throw new DomainValidationException("Err_Mcp_InvalidScope", $"Invalid value {value} for {paramName}.");
        }
        return (TEnum)(object)(int)value;
    }

    private static McpAccessListItemDto ToListItem(McpAccess a, DateTime utcNow)
        => new(a.Id, a.Name, a.Description, (short)a.Level, (short)a.ApprovalMode, (short)a.Status,
            a.ExpiresUtc, a.IsExpired(utcNow), a.LastUsedUtc, a.TokenIssuedUtc, a.RevokedUtc, a.CreatedUtc);

    private static McpAccessDetailDto ToDetail(McpAccess a, IReadOnlyList<McpAccessScope> scopes, DateTime utcNow)
        => new(a.Id, a.Name, a.Description, (short)a.Level, (short)a.ApprovalMode, (short)a.Status,
            a.ExpiresUtc, a.IsExpired(utcNow), a.AllowedIpRanges, a.LastUsedUtc, a.TokenIssuedUtc,
            a.RevokedUtc, a.RevocationReason,
            scopes.Select(s => new McpAccessScopeDto(s.Id, (short)s.DataType, (short)s.Right,
                s.AccountTypesCsv, s.ContactCategoryIdsCsv, s.SecurityCategoryIdsCsv,
                s.SavingsPlanCategoryIdsCsv, s.AttachmentCategoryIdsCsv)).ToList(),
            a.CreatedUtc, a.ModifiedUtc);
}
