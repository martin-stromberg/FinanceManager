using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Shared.Dtos.SavingsPlans;

/// <summary>
/// Request payload to create or update a savings plan.
/// Made mutable to allow UI viewmodels and tests to adjust fields.
/// </summary>
public sealed class SavingsPlanCreateRequest
{
    /// <summary>
    /// Display name of the savings plan. Required and must have at least 2 characters.
    /// </summary>
    [Required, MinLength(2)]
    public string Name { get; set; }

    /// <summary>
    /// Type of the savings plan (e.g. Goal, Contract, etc.).
    /// </summary>
    public SavingsPlanType Type { get; set; }

    /// <summary>
    /// Optional target amount for the savings plan. When null, no numeric target is used.
    /// </summary>
    public decimal? TargetAmount { get; set; }

    /// <summary>
    /// Optional target date for reaching the target amount.
    /// </summary>
    public DateTime? TargetDate { get; set; }

    /// <summary>
    /// Optional interval for contributions or aggregation (e.g. Monthly, Yearly).
    /// </summary>
    public SavingsPlanInterval? Interval { get; set; }

    /// <summary>
    /// Optional category id to group the savings plan.
    /// </summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// Optional contract number or reference associated with the savings plan.
    /// </summary>
    public string? ContractNumber { get; set; }

    /// <summary>
    /// Default constructor. Initializes string properties with safe defaults to satisfy validation.
    /// </summary>
    public SavingsPlanCreateRequest() { Name = string.Empty; }

    /// <summary>
    /// Constructs a new <see cref="SavingsPlanCreateRequest"/> with the provided values.
    /// </summary>
    /// <param name="name">Display name of the savings plan.</param>
    /// <param name="type">Type of the savings plan.</param>
    /// <param name="targetAmount">Optional target amount.</param>
    /// <param name="targetDate">Optional target date.</param>
    /// <param name="interval">Optional savings plan interval.</param>
    /// <param name="categoryId">Optional category id.</param>
    /// <param name="contractNumber">Optional contract number or reference.</param>
    public SavingsPlanCreateRequest(string name, SavingsPlanType type, decimal? targetAmount, DateTime? targetDate, SavingsPlanInterval? interval, Guid? categoryId, string? contractNumber)
    {
        Name = name;
        Type = type;
        TargetAmount = targetAmount;
        TargetDate = targetDate;
        Interval = interval;
        CategoryId = categoryId;
        ContractNumber = contractNumber;
    }
}
