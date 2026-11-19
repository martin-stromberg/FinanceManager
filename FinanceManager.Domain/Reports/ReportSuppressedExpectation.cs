namespace FinanceManager.Domain.Reports;

/// <summary>
/// Records that a single expected posting surfaced by the report projection was hidden by the
/// user for one report favorite. The suppression identity is
/// (<see cref="ReportFavoriteId"/>, <see cref="Kind"/>, <see cref="EntityId"/>,
/// <see cref="PriorYearDate"/>, <see cref="PostingId"/>) — the amount is intentionally not
/// part of the key so amount corrections on the prior-year posting do not lift the
/// suppression. The marker is bound to the prior-year date, so it stops applying naturally
/// once the reference window rolls forward into the next year; no expiry sweep is needed.
/// </summary>
public sealed class ReportSuppressedExpectation : Entity, IAggregateRoot
{
    private ReportSuppressedExpectation() { }

    /// <summary>
    /// Creates a suppression marker for one expected posting in the context of a report favorite.
    /// </summary>
    /// <param name="ownerUserId">Identifier of the user owning the favorite.</param>
    /// <param name="reportFavoriteId">Identifier of the report favorite the suppression is scoped to.</param>
    /// <param name="kind">Posting kind of the expected item.</param>
    /// <param name="entityId">Identifier of the entity (account, contact, savings plan or security) the expectation belongs to.</param>
    /// <param name="priorYearDate">Date of the prior-year posting the expectation was projected from (normalized to the date).</param>
    /// <param name="postingId">Optional identifier of the prior-year posting that produced the expectation.</param>
    /// <param name="amount">Expected amount shown for the suppressed item.</param>
    /// <exception cref="ArgumentException">Thrown when any required identifier is <see cref="Guid.Empty"/>.</exception>
    public ReportSuppressedExpectation(Guid ownerUserId, Guid reportFavoriteId, PostingKind kind, Guid entityId, DateTime priorYearDate, Guid? postingId = null, decimal? amount = null)
    {
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        ReportFavoriteId = Guards.NotEmpty(reportFavoriteId, nameof(reportFavoriteId));
        EntityId = Guards.NotEmpty(entityId, nameof(entityId));
        Kind = kind;
        PriorYearDate = priorYearDate.Date;
        PostingId = postingId;
        Amount = amount;
        SuppressedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Identifier of the user owning the suppression. Stored redundantly to simplify owner
    /// checks, indices and backup collection.
    /// </summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Identifier of the report favorite the suppression is scoped to.
    /// </summary>
    public Guid ReportFavoriteId { get; private set; }

    /// <summary>
    /// Posting kind of the suppressed expectation.
    /// </summary>
    public PostingKind Kind { get; private set; }

    /// <summary>
    /// Identifier of the entity the suppressed expectation belongs to.
    /// </summary>
    public Guid EntityId { get; private set; }

    /// <summary>
    /// Date (normalized, no time component) of the prior-year posting whose projection is suppressed.
    /// </summary>
    public DateTime PriorYearDate { get; private set; }

    /// <summary>
    /// Optional identifier of the prior-year posting that produced the suppressed expectation.
    /// Suppressions created before posting-level identity existed keep this value empty and
    /// continue to suppress all candidates of the same entity and prior-year date.
    /// </summary>
    public Guid? PostingId { get; private set; }

    /// <summary>
    /// Expected amount shown in the suppressed-expectations overlay. May be <c>null</c> for
    /// suppression markers created before the amount was persisted.
    /// </summary>
    public decimal? Amount { get; private set; }

    /// <summary>
    /// Updates the amount shown for an already suppressed expectation when a newer request
    /// supplies display metadata. The suppression identity is intentionally left unchanged.
    /// </summary>
    /// <param name="amount">Expected amount to display; <c>null</c> leaves the stored amount untouched.</param>
    public void UpdateExpectedAmount(decimal? amount)
    {
        if (amount.HasValue && Amount != amount.Value)
        {
            Amount = amount.Value;
            Touch();
        }
    }

    /// <summary>
    /// UTC timestamp when the expectation was suppressed.
    /// </summary>
    public DateTime SuppressedAtUtc { get; private set; }

    /// <summary>
    /// Navigation property to the report favorite the suppression is scoped to.
    /// </summary>
    public ReportFavorite? ReportFavorite { get; private set; }

    /// <summary>
    /// Backup DTO for <see cref="ReportSuppressedExpectation"/>.
    /// </summary>
    /// <param name="Id">Entity identifier.</param>
    /// <param name="OwnerUserId">Identifier of the owning user.</param>
    /// <param name="ReportFavoriteId">Identifier of the report favorite the suppression is scoped to.</param>
    /// <param name="Kind">Posting kind of the suppressed expectation.</param>
    /// <param name="EntityId">Identifier of the entity the suppressed expectation belongs to.</param>
    /// <param name="PriorYearDate">Date of the prior-year posting whose projection is suppressed.</param>
    /// <param name="SuppressedAtUtc">UTC timestamp when the expectation was suppressed.</param>
    /// <param name="CreatedUtc">Entity creation timestamp UTC.</param>
    /// <param name="ModifiedUtc">Entity last modified timestamp UTC, if any.</param>
    /// <param name="PostingId">Optional identifier of the prior-year posting that produced the expectation.</param>
    /// <param name="Amount">Expected amount shown for the suppressed item.</param>
    /// <returns>The result.</returns>
    public sealed record ReportSuppressedExpectationBackupDto(Guid Id, Guid OwnerUserId, Guid ReportFavoriteId, PostingKind Kind, Guid EntityId, DateTime PriorYearDate, DateTime SuppressedAtUtc, DateTime CreatedUtc, DateTime? ModifiedUtc, Guid? PostingId = null, decimal? Amount = null);

    /// <summary>
    /// Creates a backup DTO representing the serializable state of this suppression.
    /// </summary>
    /// <returns>A <see cref="ReportSuppressedExpectationBackupDto"/> containing values needed to restore the suppression.</returns>
    public ReportSuppressedExpectationBackupDto ToBackupDto()
        => new ReportSuppressedExpectationBackupDto(Id, OwnerUserId, ReportFavoriteId, Kind, EntityId, PriorYearDate, SuppressedAtUtc, CreatedUtc, ModifiedUtc, PostingId, Amount);

    /// <summary>
    /// Applies values from a backup DTO to this suppression instance.
    /// </summary>
    /// <param name="dto">The backup DTO to apply to this entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is <c>null</c>.</exception>
    public void AssignBackupDto(ReportSuppressedExpectationBackupDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        OwnerUserId = dto.OwnerUserId;
        ReportFavoriteId = dto.ReportFavoriteId;
        Kind = dto.Kind;
        EntityId = dto.EntityId;
        PriorYearDate = dto.PriorYearDate.Date;
        PostingId = dto.PostingId;
        Amount = dto.Amount;
        SuppressedAtUtc = dto.SuppressedAtUtc;
        SetDates(dto.CreatedUtc, dto.ModifiedUtc);
    }
}
