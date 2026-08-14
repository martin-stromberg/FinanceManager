using System.Text.Json;
using FinanceManager.Application.Portfolio;
using FinanceManager.Domain.Reports;
using FinanceManager.Shared.Dtos.Portfolio;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Portfolio;

/// <summary>
/// Database-backed cache for the portfolio analysis report with monthly validity, following the
/// <c>ReportCacheService</c> pattern but specialized for <see cref="PortfolioAnalysisReportDto"/> and extended
/// with a <c>CacheValidUntilUtc</c> validity check instead of a purely explicit <c>NeedsRefresh</c> flag.
/// </summary>
public sealed class PortfolioAnalysisReportCacheService : IPortfolioAnalysisReportCacheService
{
    private const string CacheKeyPrefix = "portfolio-analysis-report";

    /// <summary>
    /// Version of the <see cref="PortfolioAnalysisReportDto"/> shape written to <see cref="Domain.Reports.ReportCacheEntry.Parameter"/>.
    /// Bump this whenever the DTO gains/loses non-nullable members, so cache entries serialized under an older
    /// shape are treated as a miss instead of being deserialized into a partially-null DTO (missing JSON
    /// properties deserialize to <c>null</c> for reference-typed record parameters, even when declared non-nullable).
    /// </summary>
    private const string CacheSchemaVersion = "5";

    private readonly AppDbContext _db;
    private readonly IPortfolioAnalysisReportService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortfolioAnalysisReportCacheService"/> class.
    /// </summary>
    /// <param name="db">Application database context.</param>
    /// <param name="service">Underlying service used to (re-)compute the report on cache miss.</param>
    public PortfolioAnalysisReportCacheService(AppDbContext db, IPortfolioAnalysisReportService service)
    {
        _db = db;
        _service = service;
    }

    /// <inheritdoc />
    public async Task<PortfolioAnalysisReportDto> GetPortfolioReportAsync(Guid ownerUserId, CancellationToken ct)
    {
        var key = BuildCacheKey(ownerUserId);
        var now = DateTime.UtcNow;

        var entry = await _db.ReportCacheEntries
            .FirstOrDefaultAsync(e => e.OwnerUserId == ownerUserId && e.CacheKey == key, ct);

        if (entry != null && !entry.NeedsRefresh && entry.CacheValidUntilUtc.HasValue && entry.CacheValidUntilUtc.Value >= now
            && entry.Parameter == CacheSchemaVersion)
        {
            var cached = JsonSerializer.Deserialize<PortfolioAnalysisReportDto>(entry.CacheValue);
            if (cached != null) { return cached; }
        }

        var report = await _service.GetPortfolioAnalysisReportAsync(ownerUserId, ct);
        var json = JsonSerializer.Serialize(report);
        var validUntil = report.CacheValidUntilUtc;

        if (entry == null)
        {
            entry = new ReportCacheEntry(ownerUserId, key, json, parameter: CacheSchemaVersion, needsRefresh: false, cacheValidUntilUtc: validUntil);
            _db.ReportCacheEntries.Add(entry);
        }
        else
        {
            entry.Update(json, parameter: CacheSchemaVersion, needsRefresh: false, cacheValidUntilUtc: validUntil);
        }

        await _db.SaveChangesAsync(ct);
        return report;
    }

    /// <inheritdoc />
    public async Task InvalidateCacheAsync(Guid ownerUserId, CancellationToken ct)
    {
        var key = BuildCacheKey(ownerUserId);
        var entry = await _db.ReportCacheEntries
            .FirstOrDefaultAsync(e => e.OwnerUserId == ownerUserId && e.CacheKey == key, ct);

        if (entry == null) { return; }

        _db.ReportCacheEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Builds the cache key for the given owner. The report is cached once per user (its content already
    /// reflects the current month at computation time; validity is governed by <c>CacheValidUntilUtc</c>).
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <returns>The cache key string.</returns>
    private static string BuildCacheKey(Guid ownerUserId) => $"{CacheKeyPrefix}-{ownerUserId:N}";
}
