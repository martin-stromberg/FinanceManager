using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FluentAssertions;

namespace FinanceManager.Tests.Postings;

/// <summary>
/// Pure domain-layer unit tests for posting reversal state on the <see cref="Posting"/> entity.
/// No infrastructure dependencies – all assertions operate on in-memory objects only.
/// </summary>
public sealed class PostingReversalDomainTests
{
    private static readonly Guid UserId = new("11111111-0000-0000-0000-000000000001");
    private static readonly Guid AccountId = new("AAAAAAAA-0000-0000-0000-000000000001");

    private static Posting CreatePosting(decimal amount = 100m, string subject = "Test")
    {
        var posting = new Posting(
            sourceId: Guid.NewGuid(),
            kind: PostingKind.Bank,
            accountId: AccountId,
            contactId: null,
            savingsPlanId: null,
            securityId: null,
            bookingDate: new DateTime(2025, 1, 15),
            amount: amount,
            subject: subject,
            recipientName: "Recipient",
            description: null,
            securitySubType: null);
        posting.SetGroup(Guid.NewGuid());
        return posting;
    }

    /// <summary>
    /// L22 – SetReversedBy marks the posting as reversed and stores the reversal posting id and user id.
    /// </summary>
    [Fact]
    public void SetReversedBy_ShouldSetReversedProperties()
    {
        // Arrange
        var original = CreatePosting(100m, "Original");
        var reversal = CreatePosting(-100m, "REVERSAL: Original");

        // Act
        original.SetReversedBy(reversal, UserId);

        // Assert
        original.IsReversed.Should().BeTrue();
        original.ReversedByPostingId.Should().Be(reversal.Id);
        original.ReversedByUserId.Should().Be(UserId);
        original.ReversedAtUtc.Should().NotBeNull();
    }

    /// <summary>
    /// L23 – SetReversalFor marks the posting as a reversal and links it to the original posting.
    /// </summary>
    [Fact]
    public void SetReversalFor_ShouldSetReversalProperties()
    {
        // Arrange
        var original = CreatePosting(200m, "Original");
        var reversal = CreatePosting(-200m, "REVERSAL: Original");

        // Act
        reversal.SetReversalFor(original);

        // Assert
        reversal.IsReversal.Should().BeTrue();
        reversal.ReversalForPostingId.Should().Be(original.Id);
    }

    /// <summary>
    /// L24 – IsReversed returns false on a fresh posting that has never been reversed.
    /// </summary>
    [Fact]
    public void IsReversed_ShouldBeFalse_ForFreshPosting()
    {
        // Arrange
        var posting = CreatePosting(50m, "NotYetReversed");

        // Act & Assert
        posting.IsReversed.Should().BeFalse();
        posting.ReversedByPostingId.Should().BeNull();
        posting.ReversedByUserId.Should().BeNull();
        posting.ReversedAtUtc.Should().BeNull();
    }

    /// <summary>
    /// L25 – IsReversal returns false on a fresh posting that was not created as a reversal.
    /// </summary>
    [Fact]
    public void IsReversal_ShouldBeFalse_ForFreshPosting()
    {
        // Arrange
        var posting = CreatePosting(75m, "NotAReversal");

        // Act & Assert
        posting.IsReversal.Should().BeFalse();
        posting.ReversalForPostingId.Should().BeNull();
    }

    /// <summary>
    /// L26 – SetReversedBy throws InvalidOperationException when called on a posting that is already reversed.
    /// </summary>
    [Fact]
    public void SetReversedBy_ShouldThrow_WhenAlreadyReversed()
    {
        // Arrange
        var original = CreatePosting(100m, "Original");
        var reversal1 = CreatePosting(-100m, "First Reversal");
        var reversal2 = CreatePosting(-100m, "Second Reversal");
        original.SetReversedBy(reversal1, UserId);

        // Act
        var act = () => original.SetReversedBy(reversal2, UserId);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// L27 – SetReversalFor throws InvalidOperationException when called on a posting that is already a reversal.
    /// </summary>
    [Fact]
    public void SetReversalFor_ShouldThrow_WhenAlreadyAReversal()
    {
        // Arrange
        var original1 = CreatePosting(100m, "Original1");
        var original2 = CreatePosting(200m, "Original2");
        var reversal = CreatePosting(-100m, "Reversal");
        reversal.SetReversalFor(original1);

        // Act
        var act = () => reversal.SetReversalFor(original2);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
