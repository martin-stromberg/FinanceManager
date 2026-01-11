using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Domain.Budget;

/// <summary>
/// A budget rule defines when and how much should be expected for a purpose.
/// Rules are deterministic and do not create persisted planned postings.
/// </summary>
public sealed class BudgetRule : Entity, IAggregateRoot
{
    private BudgetRule() { }

    /// <summary>
    /// Creates a new budget rule.
    /// </summary>
    /// <param name="ownerUserId">Owner user id. Must not be empty.</param>
    /// <param name="budgetPurposeId">Budget purpose id. Must not be empty.</param>
    /// <param name="amount">Expected amount (positive or negative).</param>
    /// <param name="interval">Interval definition.</param>
    /// <param name="startDate">Start date (inclusive).</param>
    /// <param name="endDate">Optional end date (inclusive).</param>
    /// <param name="customIntervalMonths">Custom interval in months when <paramref name="interval"/> is <see cref="BudgetIntervalType.CustomMonths"/>.</param>
    public BudgetRule(
        Guid ownerUserId,
        Guid budgetPurposeId,
        decimal amount,
        BudgetIntervalType interval,
        DateOnly startDate,
        DateOnly? endDate = null,
        int? customIntervalMonths = null)
    {
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        BudgetPurposeId = Guards.NotEmpty(budgetPurposeId, nameof(budgetPurposeId));
        SetAmount(amount);
        SetSchedule(interval, startDate, endDate, customIntervalMonths);
    }

    /// <summary>
    /// Owner user identifier.
    /// </summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Budget purpose identifier.
    /// </summary>
    public Guid BudgetPurposeId { get; private set; }

    /// <summary>
    /// Expected amount.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// Interval definition.
    /// </summary>
    public BudgetIntervalType Interval { get; private set; }

    /// <summary>
    /// Custom interval in months when <see cref="Interval"/> is <see cref="BudgetIntervalType.CustomMonths"/>.
    /// </summary>
    public int? CustomIntervalMonths { get; private set; }

    /// <summary>
    /// Start date (inclusive).
    /// </summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>
    /// Optional end date (inclusive).
    /// </summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>
    /// Sets the amount.
    /// </summary>
    /// <param name="amount">Expected amount.</param>
    public void SetAmount(decimal amount)
    {
        Amount = amount;
        Touch();
    }

    /// <summary>
    /// Sets schedule values.
    /// </summary>
    /// <param name="interval">Interval definition.</param>
    /// <param name="startDate">Start date.</param>
    /// <param name="endDate">End date.</param>
    /// <param name="customIntervalMonths">Custom interval months if applicable.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when custom interval months are invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when end date is before start date.</exception>
    public void SetSchedule(BudgetIntervalType interval, DateOnly startDate, DateOnly? endDate, int? customIntervalMonths)
    {
        Interval = interval;
        CustomIntervalMonths = null;

        if (interval == BudgetIntervalType.CustomMonths)
        {
            if (customIntervalMonths == null)
            {
                throw new ArgumentOutOfRangeException(nameof(customIntervalMonths), "Custom interval months required for CustomMonths interval");
            }

            if (customIntervalMonths.Value < 1 || customIntervalMonths.Value > 120)
            {
                throw new ArgumentOutOfRangeException(nameof(customIntervalMonths), "Custom interval months must be between 1 and 120");
            }

            CustomIntervalMonths = customIntervalMonths.Value;
        }

        if (endDate != null && endDate.Value < startDate)
        {
            throw new ArgumentException("EndDate must not be earlier than StartDate", nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
        Touch();
    }

    /// <summary>
    /// Returns the step size (months) for the interval.
    /// </summary>
    /// <returns>Step size in months.</returns>
    public int GetIntervalStepMonths()
    {
        return Interval switch
        {
            BudgetIntervalType.Monthly => 1,
            BudgetIntervalType.Quarterly => 3,
            BudgetIntervalType.Yearly => 12,
            BudgetIntervalType.CustomMonths => CustomIntervalMonths ?? 1,
            _ => 1
        };
    }

    /// <summary>
    /// DTO carrying the serializable state of a <see cref="BudgetRule"/> for backup purposes.
    /// </summary>
    /// <param name="Id">Rule id.</param>
    /// <param name="OwnerUserId">Owner user id.</param>
    /// <param name="BudgetPurposeId">Budget purpose id.</param>
    /// <param name="Amount">Expected amount.</param>
    /// <param name="Interval">Interval.</param>
    /// <param name="CustomIntervalMonths">Custom interval months.</param>
    /// <param name="StartDate">Start date.</param>
    /// <param name="EndDate">Optional end date.</param>
    public sealed record BudgetRuleBackupDto(
        Guid Id,
        Guid OwnerUserId,
        Guid BudgetPurposeId,
        decimal Amount,
        BudgetIntervalType Interval,
        int? CustomIntervalMonths,
        DateOnly StartDate,
        DateOnly? EndDate);

    /// <summary>
    /// Creates a backup DTO representing the serializable state of this budget rule.
    /// </summary>
    public BudgetRuleBackupDto ToBackupDto()
        => new BudgetRuleBackupDto(Id, OwnerUserId, BudgetPurposeId, Amount, Interval, CustomIntervalMonths, StartDate, EndDate);

    /// <summary>
    /// Applies values from the provided backup DTO to this entity.
    /// </summary>
    public void AssignBackupDto(BudgetRuleBackupDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        OwnerUserId = dto.OwnerUserId;
        BudgetPurposeId = dto.BudgetPurposeId;
        Amount = dto.Amount;
        Interval = dto.Interval;
        CustomIntervalMonths = dto.CustomIntervalMonths;
        StartDate = dto.StartDate;
        EndDate = dto.EndDate;
    }
}
