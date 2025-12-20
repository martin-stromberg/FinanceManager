using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Shared.Dtos.SavingsPlans;

/// <summary>
/// Request payload to create or update a savings plan.
/// Made mutable to allow UI viewmodels and tests to adjust fields.
/// </summary>
public sealed class SavingsPlanCreateRequest
{
    [Required, MinLength(2)]
    public string Name { get; set; }
    public SavingsPlanType Type { get; set; }
    public decimal? TargetAmount { get; set; }
    public DateTime? TargetDate { get; set; }
    public SavingsPlanInterval? Interval { get; set; }
    public Guid? CategoryId { get; set; }
    public string? ContractNumber { get; set; }

    public SavingsPlanCreateRequest() { Name = string.Empty; }

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
