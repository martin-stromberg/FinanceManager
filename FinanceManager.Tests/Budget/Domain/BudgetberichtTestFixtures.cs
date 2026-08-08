using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Tests.Budget.Domain;

/// <summary>
/// Shared factory helpers for building <c>Budgetbericht</c> planning input (categories, purposes, rules)
/// and actual postings (<see cref="MonthlyBudgetRealization"/>) used across the <c>BudgetberichtTests_*</c>
/// test classes.
/// </summary>
internal static class BudgetberichtTestFixtures
{
    /// <summary>
    /// Creates a <see cref="BudgetCategoryDto"/> with a random id.
    /// </summary>
    public static BudgetCategoryDto CreateCategory(string name)
        => new(Guid.NewGuid(), Guid.NewGuid(), name);

    /// <summary>
    /// Creates a <see cref="BudgetPurposeDto"/> with a random id.
    /// </summary>
    public static BudgetPurposeDto CreatePurpose(
        string name,
        BudgetSourceType sourceType,
        Guid sourceId,
        Guid? categoryId = null,
        BudgetValuationType valuationType = BudgetValuationType.ExactPostings)
        => new(Guid.NewGuid(), Guid.NewGuid(), name, null, sourceType, sourceId, categoryId, valuationType);

    /// <summary>
    /// Creates a <see cref="BudgetRuleDto"/> for a budget purpose.
    /// </summary>
    public static BudgetRuleDto CreatePurposeRule(
        Guid purposeId,
        decimal amount,
        BudgetIntervalType interval,
        DateOnly startDate,
        DateOnly? endDate = null,
        int? customIntervalMonths = null,
        string? purposePattern = null,
        bool useRegex = false)
        => new(Guid.NewGuid(), Guid.NewGuid(), purposeId, null, amount, interval, customIntervalMonths, startDate, endDate, purposePattern, useRegex);

    /// <summary>
    /// Creates a <see cref="BudgetRuleDto"/> for a budget category (direct category-level expectation).
    /// </summary>
    public static BudgetRuleDto CreateCategoryRule(
        Guid categoryId,
        decimal amount,
        BudgetIntervalType interval,
        DateOnly startDate,
        DateOnly? endDate = null,
        int? customIntervalMonths = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), null, categoryId, amount, interval, customIntervalMonths, startDate, endDate, null, false);

    /// <summary>
    /// Creates a <see cref="MonthlyBudgetRealization"/> representing an actual posting attributed to a contact.
    /// </summary>
    public static MonthlyBudgetRealization CreateContactPosting(
        decimal amount,
        DateTime bookingDate,
        Guid contactId,
        Guid? contactGroupId = null,
        string? purpose = null,
        string? description = null,
        Guid? groupId = null,
        DateTime? valutaDate = null)
        => new()
        {
            PostingId = Guid.NewGuid(),
            BookingDate = bookingDate,
            ValutaDate = valutaDate,
            ContactId = contactId,
            ContactGroupId = contactGroupId,
            Amount = amount,
            Purpose = purpose,
            Description = description,
            GroupId = groupId
        };

    /// <summary>
    /// Creates a <see cref="MonthlyBudgetRealization"/> representing an actual posting attributed to a savings plan.
    /// </summary>
    public static MonthlyBudgetRealization CreateSavingsPlanPosting(
        decimal amount,
        DateTime bookingDate,
        Guid savingsPlanId,
        string? purpose = null,
        string? description = null,
        Guid? groupId = null)
        => new()
        {
            PostingId = Guid.NewGuid(),
            BookingDate = bookingDate,
            SavingsPlanId = savingsPlanId,
            Amount = amount,
            Purpose = purpose,
            Description = description,
            GroupId = groupId
        };

    /// <summary>
    /// Creates a <see cref="MonthlyBudgetRealization"/> that does not match any contact/contact group/savings plan
    /// source (used to exercise the unbudgeted/cost-neutral routing).
    /// </summary>
    public static MonthlyBudgetRealization CreateUnattributedPosting(
        decimal amount,
        DateTime bookingDate,
        string? purpose = null,
        Guid? groupId = null)
        => new()
        {
            PostingId = Guid.NewGuid(),
            BookingDate = bookingDate,
            Amount = amount,
            Purpose = purpose,
            GroupId = groupId
        };
}
