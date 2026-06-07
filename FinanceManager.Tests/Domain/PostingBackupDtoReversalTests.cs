using FinanceManager.Domain.Postings;
using FluentAssertions;

namespace FinanceManager.Tests.Postings;

/// <summary>
/// Unit tests for the <see cref="PostingBackupDto"/> roundtrip via <c>ToBackupDto</c> and <c>AssignBackupDto</c>.
/// </summary>
public sealed class PostingBackupDtoReversalTests
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
            bookingDate: new DateTime(2025, 3, 10),
            amount: amount,
            subject: subject,
            recipientName: "Recipient",
            description: null,
            securitySubType: null);
        posting.SetGroup(Guid.NewGuid());
        return posting;
    }

    /// <summary>
    /// L36 – ToBackupDto captures reversal fields; AssignBackupDto restores them exactly.
    /// A posting with reversal metadata must survive a full backup/restore roundtrip without data loss.
    /// </summary>
    [Fact]
    public void ToBackupDto_AndAssignBackupDto_ShouldPreserveReversalFields()
    {
        // Arrange – set up a posting that has been reversed
        var original = CreatePosting(350m, "Payment");
        var reversalPosting = CreatePosting(-350m, "REVERSAL: Payment");
        reversalPosting.SetReversalFor(original);
        original.SetReversedBy(reversalPosting, UserId);

        // Capture state via backup DTO
        var dto = original.ToBackupDto();

        // Act – restore into a fresh posting (simulate DB restore scenario)
        var restored = CreatePosting(350m, "Payment"); // same base properties
        restored.AssignBackupDto(dto);

        // Assert – reversal fields must be preserved exactly
        restored.IsReversed.Should().BeTrue();
        restored.ReversedByPostingId.Should().Be(reversalPosting.Id);
        restored.ReversedByUserId.Should().Be(UserId);
        restored.ReversedAtUtc.Should().Be(original.ReversedAtUtc);

        // Verify the reversal side as well
        var reversalDto = reversalPosting.ToBackupDto();
        var restoredReversal = CreatePosting(-350m, "REVERSAL: Payment");
        restoredReversal.AssignBackupDto(reversalDto);

        restoredReversal.IsReversal.Should().BeTrue();
        restoredReversal.ReversalForPostingId.Should().Be(original.Id);
    }
}
