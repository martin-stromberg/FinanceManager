namespace FinanceManager.Shared.Dtos.SavingsPlans;

/// <summary>
/// DTO describing a savings plan including target and interval configuration.
/// </summary>
public sealed class SavingsPlanDto
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SavingsPlanDto"/> class with default values.
    /// Use this constructor when creating an empty DTO to populate later (e.g. model binding or tests).
    /// </summary>
    public SavingsPlanDto()
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SavingsPlanDto"/> class with the specified values.
    /// </summary>
    /// <param name="id">Unique savings plan identifier.</param>
    /// <param name="name">Display name of the plan.</param>
    /// <param name="type">Type of the plan.</param>
    /// <param name="targetAmount">Optional target amount.</param>
    /// <param name="targetDate">Optional target date.</param>
    /// <param name="interval">Optional recurrence interval.</param>
    /// <param name="isActive">Indicates whether the plan is currently active.</param>
    /// <param name="createdUtc">UTC timestamp when the plan was created.</param>
    /// <param name="archivedUtc">UTC timestamp when the plan was archived, if any.</param>
    /// <param name="categoryId">Optional category id the plan belongs to.</param>
    /// <param name="contractNumber">Optional contract number associated with the plan.</param>
    /// <param name="symbolAttachmentId">Optional symbol attachment id.</param>
    /// <param name="remainingAmount">Remaining amount to reach the target (derived).</param>
    /// <param name="currentAmount">Currently accumulated amount for the plan.</param>
    public SavingsPlanDto(Guid id, string name, SavingsPlanType type, decimal? targetAmount, DateTime? targetDate, SavingsPlanInterval? interval, bool isActive, DateTime createdUtc, DateTime? archivedUtc, Guid? categoryId, string? contractNumber = null, Guid? symbolAttachmentId = null, decimal remainingAmount = 0m, decimal currentAmount = 0m)
        : this()
    {
        Id = id;
        Name = name;
        Type = type;
        TargetAmount = targetAmount;
        TargetDate = targetDate;
        IsActive = isActive;
        CreatedUtc = createdUtc;
        ArchivedUtc = archivedUtc;
        Interval = interval;
        CategoryId = categoryId;
        ContractNumber = contractNumber;
        SymbolAttachmentId = symbolAttachmentId;
        RemainingAmount = remainingAmount;
        CurrentAmount = currentAmount;
    }

    /// <summary>Unique savings plan identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>Display name of the plan.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Type of the plan.</summary>
    public SavingsPlanType Type { get; set; }
    /// <summary>Optional target amount.</summary>
    public decimal? TargetAmount { get; set; }
    /// <summary>Optional target date.</summary>
    public DateTime? TargetDate { get; set; }
    /// <summary>Optional recurrence interval.</summary>
    public SavingsPlanInterval? Interval { get; set; }
    /// <summary>Indicates whether the plan is currently active.</summary>
    public bool IsActive { get; set; }
    /// <summary>UTC timestamp when the plan was created.</summary>
    public DateTime CreatedUtc { get; set; }
    /// <summary>UTC timestamp when the plan was archived, if any.</summary>
    public DateTime? ArchivedUtc { get; set; }
    /// <summary>Optional category id the plan belongs to.</summary>
    public Guid? CategoryId { get; set; }
    /// <summary>Optional contract number associated with the plan.</summary>
    public string? ContractNumber { get; set; }

    /// <summary>Optional symbol attachment id.</summary>
    public Guid? SymbolAttachmentId { get; set; }

    /// <summary>
    /// Remaining amount to reach the target. When no target is defined this is 0.
    /// </summary>
    public decimal RemainingAmount { get; set; }

    /// <summary>
    /// Currently accumulated amount for the savings plan.
    /// </summary>
    public decimal CurrentAmount { get; set; }
}
