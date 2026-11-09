using FinanceManager.Domain.Postings;
using FluentAssertions;

namespace FinanceManager.Tests.Postings;

/// <summary>
/// Covers the reference invariant of the <see cref="Posting"/> entity: at least one of the four
/// target references (<see cref="Posting.AccountId"/>, <see cref="Posting.ContactId"/>,
/// <see cref="Posting.SavingsPlanId"/>, <see cref="Posting.SecurityId"/>) must be set.
/// The guard lives in the full constructor that all overloads delegate to.
/// </summary>
public sealed class PostingReferenceInvariantTests
{
    /// <summary>
    /// Creating a posting without any target reference throws an <see cref="ArgumentException"/>
    /// on the main constructor path and on the delegating overloads.
    /// </summary>
    [Fact]
    public void Constructor_Throws_WhenAllReferencesAreNull()
    {
        var bookingDate = new DateTime(2025, 1, 15);

        var actMain = () => new Posting(
            sourceId: Guid.NewGuid(),
            kind: PostingKind.Bank,
            accountId: null,
            contactId: null,
            savingsPlanId: null,
            securityId: null,
            bookingDate: bookingDate,
            valutaDate: bookingDate,
            amount: 100m,
            subject: null,
            recipientName: null,
            description: null,
            securitySubType: null,
            quantity: null);
        actMain.Should().Throw<ArgumentException>();

        var actBasic = () => new Posting(Guid.NewGuid(), PostingKind.Bank, null, null, null, null, bookingDate, 100m);
        actBasic.Should().Throw<ArgumentException>();

        var actDetailed = () => new Posting(Guid.NewGuid(), PostingKind.Bank, null, null, null, null, bookingDate, 100m, null, null, null, null);
        actDetailed.Should().Throw<ArgumentException>();

        var actWithQuantity = () => new Posting(Guid.NewGuid(), PostingKind.Bank, null, null, null, null, bookingDate, 100m, null, null, null, null, 1m);
        actWithQuantity.Should().Throw<ArgumentException>();

        var actWithValuta = () => new Posting(Guid.NewGuid(), PostingKind.Bank, null, null, null, null, bookingDate, bookingDate, 100m, null, null, null, null);
        actWithValuta.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Creating a posting succeeds when exactly one of the four target references is set.
    /// </summary>
    /// <param name="accountId">The account id to set, or <c>null</c>.</param>
    /// <param name="contactId">The contact id to set, or <c>null</c>.</param>
    /// <param name="savingsPlanId">The savings plan id to set, or <c>null</c>.</param>
    /// <param name="securityId">The security id to set, or <c>null</c>.</param>
    [Theory]
    [MemberData(nameof(SingleReferenceCases))]
    public void Constructor_Succeeds_WhenExactlyOneReferenceSet(Guid? accountId, Guid? contactId, Guid? savingsPlanId, Guid? securityId)
    {
        var posting = new Posting(
            sourceId: Guid.NewGuid(),
            kind: PostingKind.Bank,
            accountId: accountId,
            contactId: contactId,
            savingsPlanId: savingsPlanId,
            securityId: securityId,
            bookingDate: new DateTime(2025, 1, 15),
            amount: 100m);

        posting.AccountId.Should().Be(accountId);
        posting.ContactId.Should().Be(contactId);
        posting.SavingsPlanId.Should().Be(savingsPlanId);
        posting.SecurityId.Should().Be(securityId);
    }

    /// <summary>
    /// The four combinations in which exactly one target reference carries a value.
    /// </summary>
    /// <returns>Rows of <c>accountId</c>, <c>contactId</c>, <c>savingsPlanId</c>, <c>securityId</c> with exactly one non-null value.</returns>
    public static IEnumerable<object?[]> SingleReferenceCases()
    {
        yield return new object?[] { Guid.NewGuid(), null, null, null };
        yield return new object?[] { null, Guid.NewGuid(), null, null };
        yield return new object?[] { null, null, Guid.NewGuid(), null };
        yield return new object?[] { null, null, null, Guid.NewGuid() };
    }

    /// <summary>
    /// Creating a posting with several or all four target references set is allowed;
    /// the invariant is "at least one", not "exactly one".
    /// </summary>
    [Fact]
    public void Constructor_Succeeds_WhenMultipleReferencesSet()
    {
        var accountId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var savingsPlanId = Guid.NewGuid();
        var securityId = Guid.NewGuid();

        var allReferences = new Posting(Guid.NewGuid(), PostingKind.Bank, accountId, contactId, savingsPlanId, securityId, new DateTime(2025, 1, 15), 100m);
        allReferences.AccountId.Should().Be(accountId);
        allReferences.ContactId.Should().Be(contactId);
        allReferences.SavingsPlanId.Should().Be(savingsPlanId);
        allReferences.SecurityId.Should().Be(securityId);

        var twoReferences = new Posting(Guid.NewGuid(), PostingKind.Bank, accountId, contactId, null, null, new DateTime(2025, 1, 15), 50m);
        twoReferences.AccountId.Should().Be(accountId);
        twoReferences.ContactId.Should().Be(contactId);
        twoReferences.SavingsPlanId.Should().BeNull();
        twoReferences.SecurityId.Should().BeNull();
    }
}
