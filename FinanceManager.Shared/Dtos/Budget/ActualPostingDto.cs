using System;

namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Represents an actual contact posting with optional link to the originating budget rule/purpose.
/// </summary>
public sealed record ActualPostingDto(
    Guid PostingId,
    Guid SourceId,
    DateTime BookingDate,
    DateTime ValutaDate,
    decimal Amount,
    Guid? AccountId,
    Guid? ContactId,
    Guid? SavingsPlanId,
    Guid? GroupId,
    string? Subject,
    string? RecipientName,
    string? Description,
    Guid? BudgetRuleId,
    Guid? BudgetPurposeId);
