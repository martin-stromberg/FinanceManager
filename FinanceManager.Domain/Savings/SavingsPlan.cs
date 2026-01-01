namespace FinanceManager.Domain.Savings;

/// <summary>
/// Represents a user's savings plan which may be recurring or one-off and can have a target amount/date, category and optional contract/attachment.
/// </summary>
public sealed class SavingsPlan
{
    /// <summary>
    /// Gets the identifier of the savings plan.
    /// </summary>
    /// <value>The savings plan GUID.</value>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the owner user identifier for this savings plan.
    /// </summary>
    /// <value>The owner's user GUID.</value>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Gets the name of the savings plan.
    /// </summary>
    /// <value>The plan name.</value>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the type of the savings plan (e.g. one-off or recurring).
    /// </summary>
    /// <value>The <see cref="SavingsPlanType"/>.</value>
    public SavingsPlanType Type { get; private set; }

    /// <summary>
    /// Gets the optional target amount for the plan.
    /// </summary>
    /// <value>The target amount or <c>null</c> if not set.</value>
    public decimal? TargetAmount { get; private set; }

    /// <summary>
    /// Gets the optional target date for the plan.
    /// </summary>
    /// <value>The target date in UTC or <c>null</c> if not set.</value>
    public DateTime? TargetDate { get; private set; }

    /// <summary>
    /// Gets the interval used for recurring plans.
    /// </summary>
    /// <value>The <see cref="SavingsPlanInterval"/> or <c>null</c> for non-recurring plans.</value>
    public SavingsPlanInterval? Interval { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the plan is active.
    /// </summary>
    /// <value><c>true</c> if active; otherwise <c>false</c>.</value>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the creation timestamp (UTC) of the plan.
    /// </summary>
    /// <value>Creation time in UTC.</value>
    public DateTime CreatedUtc { get; private set; }

    /// <summary>
    /// Gets the archive timestamp (UTC) when the plan was archived, or <c>null</c> if not archived.
    /// </summary>
    /// <value>Archive time in UTC or <c>null</c>.</value>
    public DateTime? ArchivedUtc { get; private set; }

    /// <summary>
    /// Gets the optional category identifier associated with this plan.
    /// </summary>
    /// <value>Category GUID or <c>null</c>.</value>
    public Guid? CategoryId { get; private set; }

    /// <summary>
    /// Gets the optional contract number associated with this plan.
    /// </summary>
    /// <value>Contract number or <c>null</c>.</value>
    public string? ContractNumber { get; private set; }

    /// <summary>
    /// Optional reference to a symbol attachment associated with the plan.
    /// </summary>
    /// <value>Attachment GUID or <c>null</c>.</value>
    public Guid? SymbolAttachmentId { get; private set; }

    /// <summary>
    /// Creates a new instance of <see cref="SavingsPlan"/>.
    /// </summary>
    /// <param name="ownerUserId">Identifier of the owning user.</param>
    /// <param name="name">Name of the savings plan.</param>
    /// <param name="type">Type of the savings plan.</param>
    /// <param name="targetAmount">Optional target amount.</param>
    /// <param name="targetDate">Optional target date.</param>
    /// <param name="interval">Optional interval for recurring plans.</param>
    /// <param name="categoryId">Optional category identifier.</param>
    public SavingsPlan(Guid ownerUserId, string name, SavingsPlanType type, decimal? targetAmount, DateTime? targetDate, SavingsPlanInterval? interval, Guid? categoryId = null)
    {
        Id = Guid.NewGuid();
        OwnerUserId = ownerUserId;
        Name = name;
        Type = type;
        TargetAmount = targetAmount;
        TargetDate = targetDate;
        Interval = interval;
        CategoryId = categoryId;
        IsActive = true;
        CreatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Archives the savings plan and sets the archive timestamp to now (UTC).
    /// </summary>
    public void Archive()
    {
        IsActive = false;
        ArchivedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Renames the savings plan.
    /// </summary>
    /// <param name="name">The new name.</param>
    public void Rename(string name) => Name = name;

    /// <summary>
    /// Changes the plan type.
    /// </summary>
    /// <param name="type">The new <see cref="SavingsPlanType"/>.</param>
    public void ChangeType(SavingsPlanType type) => Type = type;

    /// <summary>
    /// Sets or clears the plan target amount and date.
    /// </summary>
    /// <param name="amount">Target amount to set, or <c>null</c> to clear.</param>
    /// <param name="date">Target date to set, or <c>null</c> to clear.</param>
    public void SetTarget(decimal? amount, DateTime? date) { TargetAmount = amount; TargetDate = date; }

    /// <summary>
    /// Sets the recurrence interval for the plan.</summary>
    /// <param name="interval">The recurrence interval or <c>null</c> to clear it.</param>
    public void SetInterval(SavingsPlanInterval? interval) => Interval = interval;

    /// <summary>
    /// Sets or clears the category for the plan.
    /// </summary>
    /// <param name="categoryId">Category GUID or <c>null</c> to clear.</param>
    public void SetCategory(Guid? categoryId) => CategoryId = categoryId;

    /// <summary>
    /// Sets the contract number. Leading/trailing whitespace is trimmed; whitespace-only values clear the contract number.
    /// </summary>
    /// <param name="contractNumber">The contract number string or <c>null</c> to clear.</param>
    public void SetContractNumber(string? contractNumber) => ContractNumber = string.IsNullOrWhiteSpace(contractNumber) ? null : contractNumber.Trim();

    /// <summary>
    /// Sets or clears the symbol attachment reference. Passing <see cref="Guid.Empty"/> is treated as <c>null</c>.
    /// </summary>
    /// <param name="attachmentId">Attachment GUID or <see cref="Guid.Empty"/> to clear.</param>
    public void SetSymbolAttachment(Guid? attachmentId)
    {
        SymbolAttachmentId = attachmentId == Guid.Empty ? null : attachmentId;
    }

    /// <summary>
    /// Advances the <see cref="TargetDate"/> for recurring plans while the due date is reached or passed relative to <paramref name="asOfUtc"/>.
    /// </summary>
    /// <param name="asOfUtc">Cutoff date (UTC) to compare the current target date against.</param>
    /// <returns><c>true</c> if the target date was advanced at least once; otherwise <c>false</c>.</returns>
    public bool AdvanceTargetDateIfDue(DateTime asOfUtc)
    {
        if (Type != SavingsPlanType.Recurring)
        {
            return false;
        }
        if (!Interval.HasValue || !TargetDate.HasValue)
        {
            return false;
        }

        bool changed = false;
        while (TargetDate!.Value.Date <= asOfUtc.Date)
        {
            TargetDate = AddIntervalWithMonthEndRule(TargetDate.Value, Interval!.Value);
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// Adds the specified interval to a date while preserving month-end semantics.
    /// If the original date was the last day of the month, the resulting date will also be the last day
    /// of the target month. Otherwise the day-of-month is capped to the last day of the new month.
    /// </summary>
    /// <param name="date">The original date.</param>
    /// <param name="interval">The interval to add.</param>
    /// <returns>The advanced date respecting month-end rules and preserving time-of-day and Kind.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an unsupported interval value is provided.</exception>
    private static DateTime AddIntervalWithMonthEndRule(DateTime date, SavingsPlanInterval interval)
    {
        int monthsToAdd = interval switch
        {
            SavingsPlanInterval.Monthly => 1,
            SavingsPlanInterval.BiMonthly => 2,
            SavingsPlanInterval.Quarterly => 3,
            SavingsPlanInterval.SemiAnnually => 6,
            SavingsPlanInterval.Annually => 12,
            _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, "Unsupported interval")
        };

        int originalDay = date.Day;
        bool wasMonthEnd = originalDay == DateTime.DaysInMonth(date.Year, date.Month);

        var added = date.AddMonths(monthsToAdd);
        int daysInNewMonth = DateTime.DaysInMonth(added.Year, added.Month);

        int newDay = wasMonthEnd
            ? daysInNewMonth
            : Math.Min(originalDay, daysInNewMonth);

        return new DateTime(added.Year, added.Month, newDay, date.Hour, date.Minute, date.Second, date.Millisecond, date.Kind);
    }
}