using FinanceManager.Application.Statements;
using FinanceManager.Shared.Dtos.Statements;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Statements;

/// <summary>
/// EF-based implementation of <see cref="IStatementImportQueryService"/> (F-0204): exposes
/// the committed, revision-safe statement documents (<c>StatementImport</c>/
/// <c>StatementEntry</c>) read-only. Ownership is derived via the anchor account
/// (<c>StatementImport.AccountId</c> → <c>Account.OwnerUserId</c>) exactly as the
/// derivation table of the permission model prescribes — records anchored to foreign or
/// missing accounts are invisible (empty result / <see langword="null"/>, never an error).
/// </summary>
public sealed class StatementImportQueryService : IStatementImportQueryService
{
    private const int MaxTake = 200;
    private readonly AppDbContext _db;

    /// <summary>Creates the service.</summary>
    /// <param name="db">The application database context.</param>
    public StatementImportQueryService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StatementImportListItemDto>> ListImportsAsync(
        Guid ownerUserId, Guid? accountId, DateTime? from, DateTime? to, int skip, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, MaxTake);
        skip = Math.Max(0, skip);

        var query = OwnedImports(ownerUserId);
        if (accountId.HasValue)
        {
            query = query.Where(i => i.AccountId == accountId.Value);
        }
        if (from.HasValue)
        {
            query = query.Where(i => i.ImportedAtUtc >= from.Value);
        }
        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1);
            query = query.Where(i => i.ImportedAtUtc < end);
        }

        var rows = await query
            .OrderByDescending(i => i.ImportedAtUtc)
            .ThenByDescending(i => i.Id)
            .Skip(skip)
            .Take(take)
            .Join(_db.Accounts.AsNoTracking(), i => i.AccountId, a => a.Id,
                (i, a) => new { Import = i, a.Type })
            .ToListAsync(ct);

        return rows
            .Select(r => new StatementImportListItemDto(
                r.Import.Id, r.Import.AccountId, r.Type, r.Import.Format,
                r.Import.ImportedAtUtc, r.Import.OriginalFileName, r.Import.TotalEntries))
            .ToList();
    }

    /// <inheritdoc />
    public Task<int> CountImportsAsync(Guid ownerUserId, Guid? accountId, CancellationToken ct)
    {
        var query = OwnedImports(ownerUserId);
        if (accountId.HasValue)
        {
            query = query.Where(i => i.AccountId == accountId.Value);
        }
        return query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<StatementImportListItemDto?> GetImportAsync(Guid ownerUserId, Guid importId, CancellationToken ct)
    {
        var row = await OwnedImports(ownerUserId)
            .Where(i => i.Id == importId)
            .Join(_db.Accounts.AsNoTracking(), i => i.AccountId, a => a.Id,
                (i, a) => new { Import = i, a.Type })
            .FirstOrDefaultAsync(ct);
        return row is null
            ? null
            : new StatementImportListItemDto(
                row.Import.Id, row.Import.AccountId, row.Type, row.Import.Format,
                row.Import.ImportedAtUtc, row.Import.OriginalFileName, row.Import.TotalEntries);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StatementImportEntryDto>> ListEntriesAsync(
        Guid ownerUserId, Guid importId, int skip, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, MaxTake);
        skip = Math.Max(0, skip);
        if (!await ImportVisibleAsync(ownerUserId, importId, ct))
        {
            return Array.Empty<StatementImportEntryDto>();
        }

        var rows = await _db.StatementEntries.AsNoTracking()
            .Where(e => e.StatementImportId == importId)
            .OrderBy(e => e.BookingDate)
            .ThenBy(e => e.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return rows
            .Select(e => new StatementImportEntryDto(
                e.Id, e.StatementImportId, e.BookingDate, e.ValutaDate, e.Amount, e.CurrencyCode,
                e.Subject, e.RecipientName, e.BookingDescription, e.IsAnnounced, e.IsCostNeutral,
                e.ContactId, e.SavingsPlanId, e.SecurityTransactionId,
                (StatementEntryStatus)(short)e.Status))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<int> CountEntriesAsync(Guid ownerUserId, Guid importId, CancellationToken ct)
    {
        if (!await ImportVisibleAsync(ownerUserId, importId, ct))
        {
            return 0;
        }
        return await _db.StatementEntries.AsNoTracking()
            .CountAsync(e => e.StatementImportId == importId, ct);
    }

    private IQueryable<FinanceManager.Domain.Statements.StatementImport> OwnedImports(Guid ownerUserId)
        => _db.StatementImports.AsNoTracking()
            .Where(i => _db.Accounts.Any(a => a.Id == i.AccountId && a.OwnerUserId == ownerUserId));

    private Task<bool> ImportVisibleAsync(Guid ownerUserId, Guid importId, CancellationToken ct)
        => OwnedImports(ownerUserId).AnyAsync(i => i.Id == importId, ct);
}
