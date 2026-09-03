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
    /// <param name="name">The category name.</param>
    /// <returns>A <see cref="BudgetCategoryDto"/> with freshly generated identifiers.</returns>
    public static BudgetCategoryDto CreateCategory(string name)
        => new(Guid.NewGuid(), Guid.NewGuid(), name);

    /// <summary>
    /// Creates a <see cref="BudgetPurposeDto"/> with a random id.
    /// </summary>
    /// <param name="name">The purpose name.</param>
    /// <param name="sourceType">The kind of source (contact, contact group, savings plan) the purpose is tied to.</param>
    /// <param name="sourceId">Id of the source entity identified by <paramref name="sourceType"/>.</param>
    /// <param name="categoryId">Optional category the purpose rolls up into; <see langword="null"/> for an uncategorized purpose.</param>
    /// <param name="valuationType">How actual postings are valuated against this purpose's expectation.</param>
    /// <returns>A <see cref="BudgetPurposeDto"/> with freshly generated identifiers.</returns>
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
    /// <param name="purposeId">Id of the <see cref="BudgetPurposeDto"/> the rule expresses an expectation for.</param>
    /// <param name="amount">Expected amount per interval.</param>
    /// <param name="interval">How often the expected amount recurs.</param>
    /// <param name="startDate">Date from which the rule applies.</param>
    /// <param name="endDate">Optional date after which the rule no longer applies; <see langword="null"/> means indefinitely.</param>
    /// <param name="customIntervalMonths">Number of months between occurrences when <paramref name="interval"/> is a custom interval.</param>
    /// <param name="purposePattern">Optional text/regex pattern used to match postings to this rule instead of the purpose's own postings.</param>
    /// <param name="useRegex">Whether <paramref name="purposePattern"/> should be interpreted as a regular expression.</param>
    /// <returns>A <see cref="BudgetRuleDto"/> with freshly generated identifiers, scoped to the given purpose.</returns>
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
    /// <param name="categoryId">Id of the <see cref="BudgetCategoryDto"/> the rule expresses an expectation for.</param>
    /// <param name="amount">Expected amount per interval.</param>
    /// <param name="interval">How often the expected amount recurs.</param>
    /// <param name="startDate">Date from which the rule applies.</param>
    /// <param name="endDate">Optional date after which the rule no longer applies; <see langword="null"/> means indefinitely.</param>
    /// <param name="customIntervalMonths">Number of months between occurrences when <paramref name="interval"/> is a custom interval.</param>
    /// <returns>A <see cref="BudgetRuleDto"/> with freshly generated identifiers, scoped to the given category.</returns>
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
    /// <param name="amount">The posting amount.</param>
    /// <param name="bookingDate">The booking date of the posting.</param>
    /// <param name="contactId">Id of the contact the posting is attributed to.</param>
    /// <param name="contactGroupId">Optional id of the contact group the contact belongs to, when the routing is group-based.</param>
    /// <param name="purpose">Optional purpose/subject text for the posting.</param>
    /// <param name="description">Optional free-text description for the posting.</param>
    /// <param name="groupId">Optional group id linking this posting to its cost-neutral mirror posting.</param>
    /// <param name="valutaDate">Optional value date for the posting, if different from <paramref name="bookingDate"/>.</param>
    /// <returns>A <see cref="MonthlyBudgetRealization"/> attributed to the given contact.</returns>
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
    /// <param name="amount">The posting amount.</param>
    /// <param name="bookingDate">The booking date of the posting.</param>
    /// <param name="savingsPlanId">Id of the savings plan the posting is attributed to.</param>
    /// <param name="purpose">Optional purpose/subject text for the posting.</param>
    /// <param name="description">Optional free-text description for the posting.</param>
    /// <param name="groupId">Optional group id linking this posting to its cost-neutral mirror posting.</param>
    /// <returns>A <see cref="MonthlyBudgetRealization"/> attributed to the given savings plan.</returns>
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
    /// <param name="amount">The posting amount.</param>
    /// <param name="bookingDate">The booking date of the posting.</param>
    /// <param name="purpose">Optional purpose/subject text for the posting.</param>
    /// <param name="groupId">Optional group id linking this posting to its cost-neutral mirror posting.</param>
    /// <param name="isSelfContact">
    /// Whether the posting is attributed to the owner's "Self" contact. Combined with <paramref name="groupId"/>,
    /// this identifies cost-neutral mirror postings - a <paramref name="groupId"/> alone is not sufficient
    /// (see <c>Budgetbericht.RouteUnmatchedPosting</c>): a grouped posting is only cost-neutral when it is
    /// also attributed to the Self contact.
    /// </param>
    /// <returns>A <see cref="MonthlyBudgetRealization"/> that routes as unbudgeted or cost-neutral.</returns>
    public static MonthlyBudgetRealization CreateUnattributedPosting(
        decimal amount,
        DateTime bookingDate,
        string? purpose = null,
        Guid? groupId = null,
        bool isSelfContact = false)
        => new()
        {
            PostingId = Guid.NewGuid(),
            BookingDate = bookingDate,
            Amount = amount,
            Purpose = purpose,
            GroupId = groupId,
            IsSelfContact = isSelfContact
        };
}
