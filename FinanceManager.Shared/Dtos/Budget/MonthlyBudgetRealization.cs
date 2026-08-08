using FinanceManager.Shared.Dtos.Postings;

namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Represents a single actual posting that is fed into a <c>Budgetbericht</c> calculation via
/// <c>Budgetbericht.AddPosting()</c>. Carries the metadata required to match the posting against
/// budget expectations and to render it back as part of the report output.
/// </summary>
public sealed record MonthlyBudgetRealization
{
    /// <summary>
    /// Gets the identifier of the underlying posting.
    /// </summary>
    public Guid PostingId { get; init; }

    /// <summary>
    /// Gets the booking date of the posting.
    /// </summary>
    public DateTime BookingDate { get; init; }

    /// <summary>
    /// Gets the valuta/value date of the posting, when available.
    /// </summary>
    public DateTime? ValutaDate { get; init; }

    /// <summary>
    /// Gets the id of the contact this posting is attributed to, when applicable.
    /// </summary>
    public Guid? ContactId { get; init; }

    /// <summary>
    /// Gets the id of the contact group (contact category) the posting's contact belongs to, when applicable.
    /// </summary>
    public Guid? ContactGroupId { get; init; }

    /// <summary>
    /// Gets the id of the savings plan this posting is attributed to, when applicable.
    /// </summary>
    public Guid? SavingsPlanId { get; init; }

    /// <summary>
    /// Gets the posting amount.
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// Gets the posting purpose/subject text used for <c>BudgetRule.PurposePattern</c> matching.
    /// </summary>
    public string? Purpose { get; init; }

    /// <summary>
    /// Gets the posting description, used together with <see cref="Purpose"/> for pattern matching.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the group id linking this posting to its paired ledger leg (e.g. the bank-side and
    /// contact-side leg of the same booked transaction), when applicable. This is set for essentially
    /// every booked posting and, on its own, does <b>not</b> indicate a self-contact cost-neutral mirror
    /// transfer - see <see cref="IsSelfContact"/>, which must also be true for that.
    /// </summary>
    public Guid? GroupId { get; init; }

    /// <summary>
    /// Gets whether this posting is attributed to the owner's "Self" contact. Combined with
    /// <see cref="GroupId"/>, this identifies cost-neutral mirror postings (e.g. a savings-plan
    /// contribution or an internal transfer booked against the Self contact).
    /// </summary>
    public bool IsSelfContact { get; init; }

    /// <summary>
    /// Gets the kind of the underlying posting.
    /// </summary>
    public PostingKind PostingKind { get; init; }

    /// <summary>
    /// Gets the id of the account the posting belongs to, when applicable.
    /// </summary>
    public Guid? AccountId { get; init; }

    /// <summary>
    /// Gets the display name of the account the posting belongs to, when applicable.
    /// </summary>
    public string? AccountName { get; init; }

    /// <summary>
    /// Gets the display name of the related contact, when applicable.
    /// </summary>
    public string? ContactName { get; init; }

    /// <summary>
    /// Gets the display name of the related savings plan, when applicable.
    /// </summary>
    public string? SavingsPlanName { get; init; }

    /// <summary>
    /// Gets the id of the related security, when applicable.
    /// </summary>
    public Guid? SecurityId { get; init; }

    /// <summary>
    /// Gets the display name of the related security, when applicable.
    /// </summary>
    public string? SecurityName { get; init; }
}
