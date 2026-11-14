using FinanceManager.Domain.Postings;

namespace FinanceManager.Infrastructure.Reports;

/// <summary>
/// Shared report query semantics extracted from <see cref="ReportAggregationService"/>:
/// period anchoring, comparison windows, effective-date filtering and group key parsing.
/// The functions are pure and used identically by the aggregation and the postings drill-down.
/// </summary>
internal static class ReportQuerySemantics
{
    /// <summary>
    /// Computes the latest anchoring period for the given interval relative to the analysis date.
    /// </summary>
    /// <param name="interval">Report interval.</param>
    /// <param name="analysisDate">Analysis date (already normalized to a month start).</param>
    /// <returns>Period start of the latest displayable period.</returns>
    internal static DateTime ComputeLatestPeriod(ReportInterval interval, DateTime analysisDate)
    {
        if (interval == ReportInterval.Month)
        {
            return analysisDate;
        }
        if (interval == ReportInterval.Ytd)
        {
            return new DateTime(analysisDate.Year, 1, 1);
        }
        if (interval == ReportInterval.Quarter)
        {
            var qIndex = (analysisDate.Month - 1) / 3; // 0..3
            return new DateTime(analysisDate.Year, qIndex * 3 + 1, 1);
        }
        if (interval == ReportInterval.HalfYear)
        {
            var hIndex = (analysisDate.Month - 1) / 6; // 0..1
            return new DateTime(analysisDate.Year, hIndex * 6 + 1, 1);
        }
        return new DateTime(analysisDate.Year, 1, 1);
    }

    /// <summary>
    /// Returns the length of a single report interval in months.
    /// </summary>
    /// <param name="interval">Report interval.</param>
    /// <returns>Number of months covered by one period (defaults to 12 for year-like intervals).</returns>
    internal static int IntervalLengthMonths(ReportInterval interval)
        => interval switch
        {
            ReportInterval.Month => 1,
            ReportInterval.Quarter => 3,
            ReportInterval.HalfYear => 6,
            _ => 12
        };

    /// <summary>
    /// Computes the start of the previous comparison period for the given interval.
    /// </summary>
    /// <param name="interval">Report interval.</param>
    /// <param name="d">Current period start.</param>
    /// <param name="clampMinValue">
    /// When <c>true</c> (default) <see cref="DateTime.MinValue"/> is returned unchanged; when
    /// <c>false</c> the input flows into the interval switch like any other date, which matches
    /// the pre-extraction local copy in <see cref="ReportAggregationService"/>'s netted dividend
    /// path that had no <see cref="DateTime.MinValue"/> guard. That path passes <c>false</c> to
    /// stay behavior-identical; the case is unreachable in practice because all-history queries
    /// never carry comparison columns and every emitted period anchor is a real date.
    /// </param>
    /// <returns>Start of the previous period (<see cref="DateTime.MinValue"/> stays <see cref="DateTime.MinValue"/> when clamped).</returns>
    internal static DateTime PrevPeriod(ReportInterval interval, DateTime d, bool clampMinValue = true)
    {
        if (d == DateTime.MinValue && clampMinValue)
        {
            return DateTime.MinValue;
        }
        return interval switch
        {
            ReportInterval.Month => d.AddMonths(-1),
            ReportInterval.Quarter => AlignToQuarterStart(d).AddMonths(-3),
            ReportInterval.HalfYear => AlignToHalfYearStart(d).AddMonths(-6),
            ReportInterval.Year => new DateTime(d.Year - 1, 1, 1),
            ReportInterval.Ytd => new DateTime(d.Year - 1, 1, 1),
            _ => d.AddMonths(-1)
        };

        static DateTime AlignToQuarterStart(DateTime d)
            => new DateTime(d.Year, ((d.Month - 1) / 3) * 3 + 1, 1);

        static DateTime AlignToHalfYearStart(DateTime d)
            => new DateTime(d.Year, ((d.Month - 1) / 6) * 6 + 1, 1);
    }

    /// <summary>
    /// Computes the effective-date window of a single report cell: the row anchor
    /// (<paramref name="periodStart"/>) shifted into the requested comparison column.
    /// </summary>
    /// <param name="interval">Report interval.</param>
    /// <param name="period">Value column (current, previous period or year ago).</param>
    /// <param name="periodStart">Period anchor of the displayed row.</param>
    /// <param name="analysis">Analysis date normalized to a month start.</param>
    /// <returns>Tuple of inclusive start and exclusive end; both <c>null</c> for the unbounded all-history interval.</returns>
    /// <exception cref="ArgumentException">Thrown when a comparison column is requested for the all-history interval.</exception>
    internal static (DateTime?, DateTime?) ComputeCellWindow(ReportInterval interval, ReportPostingsPeriod period, DateTime periodStart, DateTime analysis)
    {
        if (interval == ReportInterval.AllHistory)
        {
            if (period != ReportPostingsPeriod.Current)
            {
                throw new ArgumentException("Comparison columns are not available for the all-history interval.", nameof(period));
            }
            return (null, null);
        }
        if (interval == ReportInterval.Ytd)
        {
            var year = period == ReportPostingsPeriod.Current ? periodStart.Year : periodStart.Year - 1;
            return (new DateTime(year, 1, 1), new DateTime(year, analysis.Month, 1).AddMonths(1));
        }
        var anchor = period switch
        {
            ReportPostingsPeriod.Current => periodStart,
            ReportPostingsPeriod.Previous => PrevPeriod(interval, periodStart),
            _ => periodStart.AddYears(-1)
        };
        return (anchor, anchor.AddMonths(IntervalLengthMonths(interval)));
    }

    /// <summary>
    /// Restricts a postings query to an effective-date window: the effective date is the
    /// booking date, or the valuta date when <paramref name="useValutaDate"/> is set
    /// (<see cref="DateTime.MinValue"/> counts as "no valuta date" and falls back to the
    /// booking date - identical to the in-memory bucketing logic).
    /// </summary>
    /// <param name="source">Postings query to restrict.</param>
    /// <param name="useValutaDate">Whether the valuta date determines the effective date.</param>
    /// <param name="startInclusive">Inclusive lower bound; <c>null</c> = unbounded.</param>
    /// <param name="endExclusive">Exclusive upper bound; <c>null</c> = unbounded.</param>
    /// <returns>The restricted query.</returns>
    internal static IQueryable<Posting> ApplyDateWindow(IQueryable<Posting> source, bool useValutaDate, DateTime? startInclusive, DateTime? endExclusive)
    {
        if (startInclusive is null && endExclusive is null) { return source; }
        var start = startInclusive ?? DateTime.MinValue;
        var end = endExclusive ?? DateTime.MaxValue;
        return useValutaDate
            ? source.Where(p => (p.ValutaDate == DateTime.MinValue ? p.BookingDate : p.ValutaDate) >= start
                && (p.ValutaDate == DateTime.MinValue ? p.BookingDate : p.ValutaDate) < end)
            : source.Where(p => p.BookingDate >= start && p.BookingDate < end);
    }

    /// <summary>
    /// Parses the security id out of a <c>Security:{id}</c> or <c>Security:{id}:{subType}</c> group key.
    /// </summary>
    /// <param name="groupKey">Group key to parse.</param>
    /// <param name="securityId">Parsed security id on success.</param>
    /// <returns><c>true</c> when the key is a security key with a valid id.</returns>
    internal static bool TryParseSecurityKey(string groupKey, out Guid securityId)
    {
        securityId = Guid.Empty;
        if (!groupKey.StartsWith("Security:", StringComparison.Ordinal))
        {
            return false;
        }
        // Group keys may carry a posting-sub-type suffix ("Security:{id}:{SubType}") — parse only the id segment.
        var idPart = groupKey["Security:".Length..];
        var colonIndex = idPart.IndexOf(':');
        if (colonIndex >= 0)
        {
            idPart = idPart[..colonIndex];
        }
        return Guid.TryParse(idPart, out securityId);
    }

    /// <summary>
    /// Derives the posting kind from a group key.
    /// </summary>
    /// <param name="groupKey">Group key to inspect.</param>
    /// <returns>The posting kind or <c>null</c> when the key format is unknown.</returns>
    internal static PostingKind? ParseKindFromKey(string groupKey)
    {
        // Formats:
        // Type:Kind
        // Category:Kind:<id|_none>
        // Account:GUID / Contact:GUID / SavingsPlan:GUID / Security:GUID (derive kind)
        if (groupKey.StartsWith("Type:"))
        {
            var part = groupKey.Split(':')[1];
            // Enum.TryParse accepts numeric names of undefined members ("Type:5") - reject them.
            if (Enum.TryParse<PostingKind>(part, out var pk) && Enum.IsDefined(pk)) { return pk; }
            return null;
        }
        if (groupKey.StartsWith("Category:"))
        {
            var part = groupKey.Split(':')[1];
            if (Enum.TryParse<PostingKind>(part, out var pk) && Enum.IsDefined(pk)) { return pk; }
            return null;
        }
        if (groupKey.StartsWith("Account:")) return PostingKind.Bank;
        if (groupKey.StartsWith("Contact:")) return PostingKind.Contact;
        if (groupKey.StartsWith("SavingsPlan:")) return PostingKind.SavingsPlan;
        if (groupKey.StartsWith("Security:")) return PostingKind.Security;
        return null;
    }

    /// <summary>
    /// Parses a report group key into a discriminated selection target.
    /// </summary>
    /// <param name="groupKey">Group key of a report row; <c>null</c>/empty means the total row.</param>
    /// <param name="target">Parsed target on success.</param>
    /// <returns><c>true</c> for a well-known key format; <c>false</c> for unknown or malformed keys.</returns>
    internal static bool TryParseGroupKey(string? groupKey, out ReportGroupKeyTarget target)
    {
        target = new ReportGroupKeyTarget(ReportGroupKeyTargetKind.Total, null, null, null, null);
        if (string.IsNullOrWhiteSpace(groupKey))
        {
            return true;
        }

        var parts = groupKey.Split(':');
        switch (parts[0])
        {
            case "Type":
                // Enum.TryParse accepts numeric names of undefined members ("Type:5") - reject them.
                if (parts.Length == 2 && Enum.TryParse<PostingKind>(parts[1], out var typeKind) && Enum.IsDefined(typeKind))
                {
                    target = new ReportGroupKeyTarget(ReportGroupKeyTargetKind.Type, typeKind, null, null, null);
                    return true;
                }
                return false;
            case "Category":
                if (parts.Length == 3 && Enum.TryParse<PostingKind>(parts[1], out var catKind) && Enum.IsDefined(catKind)
                    && (parts[2] == "_none" || Guid.TryParse(parts[2], out _)))
                {
                    var catId = parts[2] == "_none" ? (Guid?)null : Guid.Parse(parts[2]);
                    target = new ReportGroupKeyTarget(ReportGroupKeyTargetKind.Category, catKind, null, catId, null);
                    return true;
                }
                return false;
            case "Account":
            case "Contact":
            case "SavingsPlan":
            case "Security":
                var kind = parts[0] switch
                {
                    "Account" => PostingKind.Bank,
                    "Contact" => PostingKind.Contact,
                    "SavingsPlan" => PostingKind.SavingsPlan,
                    _ => PostingKind.Security
                };
                if (parts.Length < 2 || parts.Length > 3 || !Guid.TryParse(parts[1], out var entityId))
                {
                    return false;
                }
                SecurityPostingSubType? subType = null;
                if (parts.Length == 3)
                {
                    if (kind != PostingKind.Security
                        || !Enum.TryParse<SecurityPostingSubType>(parts[2], out var parsed)
                        || !Enum.IsDefined(parsed))
                    {
                        return false;
                    }
                    subType = parsed;
                }
                target = new ReportGroupKeyTarget(ReportGroupKeyTargetKind.Entity, kind, entityId, null, subType);
                return true;
            default:
                return false;
        }
    }
}

/// <summary>
/// The kind of selection target a report group key resolves to.
/// </summary>
internal enum ReportGroupKeyTargetKind
{
    /// <summary>Total row (no group key): all postings of the requested kinds.</summary>
    Total,
    /// <summary><c>Type:{kind}</c> row: all postings of one kind.</summary>
    Type,
    /// <summary><c>Category:{kind}:{id|_none}</c> row: postings of entities within one category.</summary>
    Category,
    /// <summary>Entity row such as <c>Account:{id}</c> or <c>Security:{id}:{subType}</c>.</summary>
    Entity
}

/// <summary>
/// Discriminated selection target parsed from a report group key.
/// </summary>
/// <param name="Kind">Target kind.</param>
/// <param name="PostingKind">Posting kind for type/category/entity targets; <c>null</c> for the total row.</param>
/// <param name="EntityId">Entity id for entity targets.</param>
/// <param name="CategoryId">Category id for category targets (<c>null</c> = <c>_none</c>).</param>
/// <param name="SecuritySubType">Optional security posting sub type of an entity target.</param>
/// <returns>The parsed selection target.</returns>
internal sealed record ReportGroupKeyTarget(
    ReportGroupKeyTargetKind Kind,
    PostingKind? PostingKind,
    Guid? EntityId,
    Guid? CategoryId,
    SecurityPostingSubType? SecuritySubType);
