using System.ComponentModel.DataAnnotations;
using FinanceManager.Shared.Dtos.Common;

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
    /// Optional parent context used for server-side assignment.
    /// </summary>
    public ParentLinkRequest? Parent { get; set; }

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

/// <summary>
/// Request payload to capture an actual-balance report ("Istmeldung") for a savings plan without
/// linked accounts. The system books the difference between the reported balance and the current
/// accumulated amount as a correction posting - no account movement is created.
/// </summary>
public sealed class SavingsPlanActualReportRequest
{
    /// <summary>
    /// The real balance reported by the user. The resulting posting amounts to
    /// <c>ReportedBalance - accumulated</c> and may be negative.
    /// </summary>
    [Required]
    public decimal ReportedBalance { get; set; }

    /// <summary>
    /// Default constructor for model binding.
    /// </summary>
    public SavingsPlanActualReportRequest() { }

    /// <summary>
    /// Constructs a new <see cref="SavingsPlanActualReportRequest"/> with the reported balance.
    /// </summary>
    /// <param name="reportedBalance">The real balance reported by the user.</param>
    public SavingsPlanActualReportRequest(decimal reportedBalance)
    {
        ReportedBalance = reportedBalance;
    }
}

/// <summary>
/// Response returned after an actual-balance report ("Istmeldung") was captured.
/// </summary>
/// <param name="PostingId">Identifier of the created correction posting.</param>
/// <param name="DifferenceAmount">Amount of the created correction posting (ReportedBalance - previous accumulated).</param>
/// <param name="AccumulatedAmount">Accumulated plan amount after the correction (equals the reported balance).</param>
/// <returns>The result.</returns>
public sealed record SavingsPlanActualReportResponse(Guid PostingId, decimal DifferenceAmount, decimal AccumulatedAmount);
