using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<McpAccessService> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="McpAccessService"/>.
    /// </summary>
    /// <param name="db">Application database context.</param>
    /// <param name="tokenIssuer">Token issuer for creation and rotation.</param>
    /// <param name="scopeEvaluator">Evaluator used to render effective profiles.</param>
    /// <param name="auditWriter">Hash-chained audit write path (F-0301/F-0302); audit bundles are staged
    /// and commit atomically with the access change in one <c>SaveChangesAsync</c>.</param>
    /// <param name="logger">Logger.</param>
    public McpAccessService(
        AppDbContext db,
        IMcpTokenIssuer tokenIssuer,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        ILogger<McpAccessService> logger)
    {
        _db = db;
        _tokenIssuer = tokenIssuer;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
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
        access.Update(name, request.Description, level, approvalMode, request.ExpiresUtc, request.AllowedIpRanges);
        await StageAuditAsync(access, "AccessUpdated", ct);
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
        var newScopes = scopeDtos.Select(s => ToScope(access, s)).ToList();
        await _db.McpAccessScopes.AddRangeAsync(newScopes, ct);
        access.ReplaceIndividualScopes(newScopes);
        await StageAuditAsync(access, "ScopesChanged", ct);
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
        scope.SetFilters(request.AccountTypesCsv, request.ContactCategoryIdsCsv,
            request.SecurityCategoryIdsCsv, request.SavingsPlanCategoryIdsCsv, request.AttachmentCategoryIdsCsv);
        var access = await _db.McpAccesses.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (access is null)
        {
            // The scope references the access — a missing access row is inconsistent and
            // must not persist a filter change without its audit record.
            return false;
        }
        await StageAuditAsync(access, "FiltersChanged", ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public Task<bool> ActivateAsync(Guid ownerUserId, Guid id, CancellationToken ct)
        => ChangeStatusAsync(ownerUserId, id, a => a.Activate(), "AccessActivated", ct);

    /// <inheritdoc />
    public Task<bool> DeactivateAsync(Guid ownerUserId, Guid id, CancellationToken ct)
        => ChangeStatusAsync(ownerUserId, id, a => a.Deactivate(), "AccessDeactivated", ct);

    private async Task<bool> ChangeStatusAsync(Guid ownerUserId, Guid id, Action<McpAccess> change, string eventType, CancellationToken ct)
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
    private Task<McpAuditEntry?> StageAuditAsync(McpAccess access, string eventType, CancellationToken ct)
        => _auditWriter.StageAsync(new McpAuditWriteRequest(
            Category: McpAuditCategory.AccessEvent,
            EventType: eventType,
            ResultStatus: "Success",
            OwnerUserId: access.OwnerUserId,
            McpAccessId: access.Id,
            McpAccessName: access.Name,
            Context: "Ui"), ct);

    private static void ValidateScopes(IReadOnlyList<McpAccessScopeDto> scopes)
    {
        foreach (var scope in scopes)
        {
            var dataType = CheckedEnum<McpDataType>(scope.DataType, nameof(scope.DataType));
            var right = CheckedEnum<McpRightKind>(scope.Right, nameof(scope.Right));
            if (!McpScopeMatrix.IsValidCombination(dataType, right))
            {
                throw new DomainValidationException("Err_Mcp_InvalidScope",
                    $"The scope ({dataType}, {right}) is not a valid F3-matrix combination.");
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
