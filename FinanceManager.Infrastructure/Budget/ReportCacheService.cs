using System.Globalization;
using System.Text.Json;
using FinanceManager.Application;
using FinanceManager.Application.Budget;
using FinanceManager.Domain.Reports;
using FinanceManager.Shared.Dtos.Admin;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Budget;

/// <summary>
/// Service providing database-backed caching for report data.
/// </summary>
public sealed class ReportCacheService : IReportCacheService
{
    private readonly AppDbContext _db;
    private readonly IBackgroundTaskManager _taskManager;

    /// <summary>
    /// Creates a new instance of the <see cref="ReportCacheService"/>.
    /// </summary>
    /// <param name="db">Database context.</param>
    public ReportCacheService(AppDbContext db, IBackgroundTaskManager taskManager)
    {
        _db = db;
        _taskManager = taskManager;
    }

    /// <inheritdoc />
    public async Task<BudgetReportRawDataDto?> GetBudgetReportRawDataAsync(
        Guid ownerUserId,
        DateOnly from,
        DateOnly to,
        BudgetReportDateBasis dateBasis,
        CancellationToken ct)
    {
        var key = BuildKey(from, to, dateBasis);
        var entry = await _db.ReportCacheEntries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId && x.CacheKey == key, ct);

        if (entry == null || entry.NeedsRefresh)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(entry.Parameter))
        {
            return null;
        }

        var parameter = JsonSerializer.Deserialize<BudgetReportCacheParameter>(entry.Parameter);
        if (parameter == null || parameter.From != from || parameter.To != to || parameter.DateBasis != dateBasis)
        {
            return null;
        }

        return JsonSerializer.Deserialize<BudgetReportRawDataDto>(entry.CacheValue);
    }

    /// <inheritdoc />
    public async Task SetBudgetReportRawDataAsync(
        Guid ownerUserId,
        DateOnly from,
        DateOnly to,
        BudgetReportDateBasis dateBasis,
        BudgetReportRawDataDto data,
        bool needsRefresh,
        CancellationToken ct)
    {
        var key = BuildKey(from, to, dateBasis);
        var json = JsonSerializer.Serialize(data);
        var parameter = new BudgetReportCacheParameter(from, to, dateBasis);
        var parameterJson = JsonSerializer.Serialize(parameter);

        var entry = await _db.ReportCacheEntries
            .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId && x.CacheKey == key, ct);

        if (entry == null)
        {
            entry = new ReportCacheEntry(ownerUserId, key, json, parameterJson, needsRefresh);
            _db.ReportCacheEntries.Add(entry);
        }
        else
        {
            entry.Update(json, parameterJson, needsRefresh);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task MarkAllReportCacheEntriesForUpdateAsync(Guid ownerUserId, CancellationToken ct)
    {
        var entries = await _db.ReportCacheEntries
            .Where(x => x.OwnerUserId == ownerUserId)
            .Where(x => !x.NeedsRefresh)
            .Where(x => x.CacheKey.StartsWith(KeyPrefix_BudgetReportRawData))
            .ToListAsync(ct);

        if (entries.Count == 0)
        {
            return;
        }

        foreach (var entry in entries)
        {
            entry.MarkForRefresh();
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ClearReportCacheAsync(Guid ownerUserId, CancellationToken ct)
    {
        var entries = await _db.ReportCacheEntries
            .Where(x => x.OwnerUserId == ownerUserId)
            .ToListAsync(ct);

        if (entries.Count == 0)
        {
            return;
        }

        _db.ReportCacheEntries.RemoveRange(entries);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public BackgroundTaskInfo EnqueueBudgetReportCacheRefresh(Guid ownerUserId)
    {
        return _taskManager.Enqueue(BackgroundTaskType.RefreshBudgetReportCache, ownerUserId, payload: null, allowDuplicate: false);
    }

    /// <inheritdoc />
    public async Task<BudgetReportCacheParameter?> GetNextBudgetReportCacheToUpdateAsync(CancellationToken ct)
    {
        var entry = await _db.ReportCacheEntries.AsNoTracking()
            .Where(x => x.NeedsRefresh)
            .OrderBy(x => x.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        if (entry == null || string.IsNullOrWhiteSpace(entry.Parameter))
        {
            return null;
        }

        return JsonSerializer.Deserialize<BudgetReportCacheParameter>(entry.Parameter);
    }

    /// <inheritdoc />
    public async Task MarkBudgetReportCacheEntriesForUpdateAsync(DateOnly periodFrom, DateOnly periodTo, CancellationToken ct)
    {
        var entries = await _db.ReportCacheEntries
            .Where(x => !x.NeedsRefresh)
            .Where(x => x.CacheKey.StartsWith(KeyPrefix_BudgetReportRawData))
            .ToListAsync(ct);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Parameter))
            {
                continue;
            }

            var parameter = JsonSerializer.Deserialize<BudgetReportCacheParameter>(entry.Parameter);
            if (parameter == null)
            {
                continue;
            }

            if (periodFrom <= parameter.To && periodTo >= parameter.From)
            {
                entry.MarkForRefresh();
            }
        }

        await _db.SaveChangesAsync(ct);
    }
    private const string KeyPrefix_BudgetReportRawData = "budgetreportraw";
    private static string BuildKey(DateOnly from, DateOnly to, BudgetReportDateBasis dateBasis)
        => string.Format(CultureInfo.InvariantCulture, $"{KeyPrefix_BudgetReportRawData}-{from:yyyyMMdd}-{to:yyyyMMdd}-{dateBasis}");
}
