using FinanceManager.Application.Securities;
using FinanceManager.Domain.Postings;
using FinanceManager.Infrastructure;
using FinanceManager.Shared.Dtos.Common;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Securities;

/// <summary>
/// Provides read-only reporting queries for securities.
/// </summary>
public sealed class SecurityReportService : ISecurityReportService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Creates a new instance of <see cref="SecurityReportService"/>.
    /// </summary>
    /// <param name="db">Application database context.</param>
    public SecurityReportService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AggregatePointDto>> GetDividendAggregatesAsync(Guid ownerUserId, CancellationToken ct)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("Owner user id is required", nameof(ownerUserId));
        }

        const int SecurityPostingSubType_Dividend = (int)SecurityPostingSubType.Dividend;
        

        var today = DateTime.UtcNow.Date;
        var start = new DateTime(today.Year - 1, 1, 1);

        var securityIds = await _db.Securities
            .AsNoTracking()
            .Where(s => s.OwnerUserId == ownerUserId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        if (securityIds.Count == 0)
        {
            return Array.Empty<AggregatePointDto>();
        }

        var raw = await _db.Postings
            .AsNoTracking()
            .Where(p => p.Kind == PostingKind.Security)
            .Where(p => p.SecuritySubType.HasValue && (int)p.SecuritySubType.Value == SecurityPostingSubType_Dividend)
            .Where(p => p.SecurityId != null && securityIds.Contains(p.SecurityId.Value))
            .Where(p => p.BookingDate >= start)
            .Select(p => new { p.BookingDate, p.Amount })
            .ToListAsync(ct);

        static DateTime QuarterStart(DateTime d)
        {
            var qMonth = ((d.Month - 1) / 3) * 3 + 1;
            return new DateTime(d.Year, qMonth, 1);
        }

        return raw
            .GroupBy(x => QuarterStart(x.BookingDate))
            .Select(g => new AggregatePointDto(g.Key, g.Sum(x => x.Amount)))
            .OrderBy(x => x.PeriodStart)
            .ToList();
    }
}
