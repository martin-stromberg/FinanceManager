using FinanceManager.Application.Mcp;
using FinanceManager.Domain;
using FinanceManager.Domain.Mcp;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// EF-based implementation of <see cref="IMcpScopeEvaluator"/>: evaluates the MCP permission
/// stages 0–3 against the live database (berechtigungsmodell.md §8). Ownership is resolved
/// via the derivation table (postings per kind, statement entries via import→account, draft
/// entries via draft, alias names via contact, security prices via security), the
/// <see cref="FinanceManager.Domain.Accounts.AccountShare"/> role acts as a hard record-level lid, and scope filters are
/// evaluated with AND over dimensions / OR over scope rows where unclassified records are
/// never matched by a whitelist.
/// </summary>
public sealed class McpScopeEvaluator : IMcpScopeEvaluator
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Creates a new instance of <see cref="McpScopeEvaluator"/>.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public McpScopeEvaluator(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<McpAccessScope>> GetEffectiveScopesAsync(McpAccess access, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (access.Level != McpAccessLevel.Individuell)
        {
            // Standard levels derive their profile from the server-side level matrix at
            // runtime; the returned rows are detached descriptors (filter-less). The
            // Administration tuples resolve against the owner's live role membership —
            // a demoted owner loses the installation scope without a persisted migration.
            var isAdmin = await McpAdminRole.IsAdminAsync(_db, access.OwnerUserId, ct);
            return McpLevelProfiles.GetDefaultScopes(access.Level, isAdmin)
                .Select(t => new McpAccessScope(access.Id, access.OwnerUserId, t.DataType, t.Right))
                .ToList();
        }

        var persisted = await _db.McpAccessScopes.AsNoTracking()
            .Where(s => s.McpAccessId == access.Id)
            .OrderBy(s => s.DataType).ThenBy(s => s.Right)
            .ToListAsync(ct);
        // Persisted Administration rows may survive a demotion (granted while the owner
        // held the role) — they must not keep granting: the effective profile drops them.
        if (persisted.Any(s => s.DataType == McpDataType.Administration)
            && !await McpAdminRole.IsAdminAsync(_db, access.OwnerUserId, ct))
        {
            persisted = persisted.Where(s => s.DataType != McpDataType.Administration).ToList();
        }
        return persisted;
    }

    /// <inheritdoc />
    public async Task<bool> IsAllowedAsync(McpAccess access, McpDataType dataType, McpRightKind right, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (!access.IsCurrentlyUsable(DateTime.UtcNow))
        {
            return false;
        }
        var profile = await GetEffectiveScopesAsync(access, ct);
        return profile.Any(s => s.DataType == dataType && s.Right == right);
    }

    /// <inheritdoc />
    public async Task<McpAccessVerdict> CheckRecordAccessAsync(McpAccess access, McpDataType dataType, McpRightKind right, McpRecordContext record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(record);
        if (!access.IsCurrentlyUsable(DateTime.UtcNow))
        {
            return McpAccessVerdict.Denied;
        }

        // Stage 0 (Benutzerrecht): every supplied anchor must resolve to the owner or to an
        // active share; unresolved/foreign anchors make the record invisible (NotFound).
        var maxRightViaShare = (McpRightKind?)null;
        if (record.OwnerUserId.HasValue && record.OwnerUserId.Value != access.OwnerUserId)
        {
            return McpAccessVerdict.NotFound;
        }

        if (record.AccountId.HasValue)
        {
            var outcome = await ResolveAccountAnchorAsync(record.AccountId.Value, access.OwnerUserId, ct);
            if (outcome == AccountAnchorOutcome.NotFound)
            {
                return McpAccessVerdict.NotFound;
            }
            maxRightViaShare = outcome switch
            {
                AccountAnchorOutcome.SharedRead => Min(maxRightViaShare, McpRightKind.Read),
                AccountAnchorOutcome.SharedWrite => Min(maxRightViaShare, McpRightKind.Book),
                AccountAnchorOutcome.SharedAdmin => maxRightViaShare, // Admin lid allows all rights
                _ => maxRightViaShare
            };
        }

        if (record.ContactId.HasValue)
        {
            var owned = await _db.Contacts.AsNoTracking()
                .AnyAsync(c => c.Id == record.ContactId.Value && c.OwnerUserId == access.OwnerUserId, ct);
            if (!owned)
            {
                return McpAccessVerdict.NotFound;
            }
        }

        if (record.SavingsPlanId.HasValue)
        {
            var owned = await _db.SavingsPlans.AsNoTracking()
                .AnyAsync(s => s.Id == record.SavingsPlanId.Value && s.OwnerUserId == access.OwnerUserId, ct);
            if (!owned)
            {
                return McpAccessVerdict.NotFound;
            }
        }

        if (record.SecurityId.HasValue)
        {
            var owned = await _db.Securities.AsNoTracking()
                .AnyAsync(s => s.Id == record.SecurityId.Value && s.OwnerUserId == access.OwnerUserId, ct);
            if (!owned)
            {
                return McpAccessVerdict.NotFound;
            }
        }

        if (record.StatementImportId.HasValue)
        {
            var accountId = await _db.StatementImports.AsNoTracking()
                .Where(i => i.Id == record.StatementImportId.Value)
                .Select(i => (Guid?)i.AccountId)
                .FirstOrDefaultAsync(ct);
            if (!accountId.HasValue)
            {
                return McpAccessVerdict.NotFound;
            }
            var outcome = await ResolveAccountAnchorAsync(accountId.Value, access.OwnerUserId, ct);
            if (outcome == AccountAnchorOutcome.NotFound)
            {
                return McpAccessVerdict.NotFound;
            }
            maxRightViaShare = outcome switch
            {
                AccountAnchorOutcome.SharedRead => Min(maxRightViaShare, McpRightKind.Read),
                AccountAnchorOutcome.SharedWrite => Min(maxRightViaShare, McpRightKind.Book),
                _ => maxRightViaShare
            };
        }

        if (record.DraftId.HasValue)
        {
            var owned = await _db.StatementDrafts.AsNoTracking()
                .AnyAsync(d => d.Id == record.DraftId.Value && d.OwnerUserId == access.OwnerUserId, ct);
            if (!owned)
            {
                return McpAccessVerdict.NotFound;
            }
        }

        // Stage 3 (share lid): the share role caps the exercisable right on account-bound data.
        if (maxRightViaShare.HasValue && right > maxRightViaShare.Value)
        {
            return McpAccessVerdict.Denied;
        }

        // Stage 1 (scope tuple): effective profile must contain (dataType, right).
        var profile = await GetEffectiveScopesAsync(access, ct);
        var matching = profile.Where(s => s.DataType == dataType && s.Right == right).ToList();
        if (matching.Count == 0)
        {
            return McpAccessVerdict.Denied;
        }

        // Stage 2 (filters): OR over matching scope rows, AND over the five dimensions;
        // an unset dimension does not restrict, a set CSV is a positive whitelist and an
        // unclassified record (null value) is never matched.
        foreach (var scope in matching)
        {
            if (FiltersMatch(scope, record))
            {
                return McpAccessVerdict.Allowed;
            }
        }
        return McpAccessVerdict.Denied;
    }

    private enum AccountAnchorOutcome
    {
        NotFound,
        Owned,
        SharedRead,
        SharedWrite,
        SharedAdmin
    }

    private async Task<AccountAnchorOutcome> ResolveAccountAnchorAsync(Guid accountId, Guid ownerUserId, CancellationToken ct)
    {
        var ownerId = await _db.Accounts.AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => (Guid?)a.OwnerUserId)
            .FirstOrDefaultAsync(ct);
        if (!ownerId.HasValue)
        {
            return AccountAnchorOutcome.NotFound;
        }
        if (ownerId.Value == ownerUserId)
        {
            return AccountAnchorOutcome.Owned;
        }

        var role = await _db.AccountShares.AsNoTracking()
            .Where(s => s.AccountId == accountId && s.UserId == ownerUserId && s.RevokedUtc == null)
            .Select(s => (AccountShareRole?)s.Role)
            .FirstOrDefaultAsync(ct);
        return role switch
        {
            AccountShareRole.Read => AccountAnchorOutcome.SharedRead,
            AccountShareRole.Write => AccountAnchorOutcome.SharedWrite,
            AccountShareRole.Admin => AccountAnchorOutcome.SharedAdmin,
            _ => AccountAnchorOutcome.NotFound
        };
    }

    private static McpRightKind Min(McpRightKind? current, McpRightKind candidate)
        => !current.HasValue || candidate < current.Value ? candidate : current.Value;

    private static bool FiltersMatch(McpAccessScope scope, McpRecordContext record)
    {
        return CsvMatchesShort(scope.AccountTypesCsv, record.AccountTypeValue)
            && CsvMatchesGuid(scope.ContactCategoryIdsCsv, record.ContactCategoryId)
            && CsvMatchesGuid(scope.SecurityCategoryIdsCsv, record.SecurityCategoryId)
            && CsvMatchesGuid(scope.SavingsPlanCategoryIdsCsv, record.SavingsPlanCategoryId)
            && CsvMatchesGuid(scope.AttachmentCategoryIdsCsv, record.AttachmentCategoryId);
    }

    private static bool CsvMatchesShort(string? csv, short? value)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return true; // unset dimension does not restrict
        }
        if (!value.HasValue)
        {
            return false; // unclassified records are not matched by a whitelist
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
