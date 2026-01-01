namespace FinanceManager.Domain.Postings;

using FinanceManager.Shared.Dtos.Securities;

/// <summary>
/// Period granularity used for aggregating postings (month, quarter, half-year, year).
/// </summary>
public enum AggregatePeriod
{
    /// <summary>
    /// Monthly aggregation.
    /// </summary>
    Month = 0,

    /// <summary>
    /// Quarterly aggregation.
    /// </summary>
    Quarter = 1,

    /// <summary>
    /// Half-year aggregation.
    /// </summary>
    HalfYear = 2,

    /// <summary>
    /// Yearly aggregation.
    /// </summary>
    Year = 3
}

/// <summary>
/// Date kind used to determine which date field of a posting to use for aggregation.
/// </summary>
public enum AggregateDateKind
{
    /// <summary>
    /// Use the booking date for aggregation.
    /// </summary>
    Booking = 0,

    /// <summary>
    /// Use the valuta/value date for aggregation.
    /// </summary>
    Valuta = 1
}

/// <summary>
/// Aggregate entity that holds aggregated posting amounts for a specific period and grouping.
/// </summary>
public sealed class PostingAggregate : Entity, IAggregateRoot
{
    private PostingAggregate() { }

    /// <summary>
    /// Constructs a new posting aggregate for the specified grouping and period.
    /// </summary>
    /// <param name="kind">Posting kind this aggregate belongs to.</param>
    /// <param name="accountId">Optional account id included in the grouping.</param>
    /// <param name="contactId">Optional contact id included in the grouping.</param>
    /// <param name="savingsPlanId">Optional savings plan id included in the grouping.</param>
    /// <param name="securityId">Optional security id included in the grouping.</param>
    /// <param name="periodStart">Start date/time of the period this aggregate represents.</param>
    /// <param name="period">Aggregation period granularity.</param>
    /// <param name="securitySubType">Optional security posting sub-type.</param>
    /// <param name="dateKind">Which posting date kind to use for this aggregate.</param>
    public PostingAggregate(
        PostingKind kind,
        Guid? accountId,
        Guid? contactId,
        Guid? savingsPlanId,
        Guid? securityId,
        DateTime periodStart,
        AggregatePeriod period,
        SecurityPostingSubType? securitySubType = null,
        AggregateDateKind dateKind = AggregateDateKind.Booking)
    {
        Kind = kind;
        AccountId = accountId;
        ContactId = contactId;
        SavingsPlanId = savingsPlanId;
        SecurityId = securityId;
        SecuritySubType = securitySubType;
        PeriodStart = periodStart.Date;
        Period = period;
        DateKind = dateKind;
        Amount = 0m;
    }

    /// <summary>
    /// Kind of postings aggregated by this entity.
    /// </summary>
    public PostingKind Kind { get; private set; }

    /// <summary>
    /// Optional account identifier that is part of the grouping key.
    /// </summary>
    public Guid? AccountId { get; private set; }

    /// <summary>
    /// Optional contact identifier that is part of the grouping key.
    /// </summary>
    public Guid? ContactId { get; private set; }

    /// <summary>
    /// Optional savings plan identifier that is part of the grouping key.
    /// </summary>
    public Guid? SavingsPlanId { get; private set; }

    /// <summary>
    /// Optional security identifier that is part of the grouping key.
    /// </summary>
    public Guid? SecurityId { get; private set; }

    /// <summary>
    /// Optional sub-type for security postings used to further subdivide aggregates.
    /// </summary>
    public SecurityPostingSubType? SecuritySubType { get; private set; }

    /// <summary>
    /// Indicates which date kind (booking or valuta) is used for this aggregate.
    /// </summary>
    public AggregateDateKind DateKind { get; private set; }

    /// <summary>
    /// Start date of the aggregated period.
    /// </summary>
    public DateTime PeriodStart { get; private set; }

    /// <summary>
    /// Aggregation period granularity.
    /// </summary>
    public AggregatePeriod Period { get; private set; }

    /// <summary>
    /// Aggregated amount for the period and grouping key.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// Adds the specified delta to the aggregate amount.
    /// </summary>
    /// <param name="delta">Amount to add (may be negative).</param>
    public void Add(decimal delta)
    {
        if (delta == 0m) return;
        Amount += delta;
        Touch();
    }
}
