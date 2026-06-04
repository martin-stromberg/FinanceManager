using FinanceManager.Application.Aggregates;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Statements;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Postings;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Infrastructure.Postings;

/// <summary>
/// Unit tests for <see cref="PostingReversalService"/>.
/// Each test uses a fresh InMemory database to avoid state leakage.
/// </summary>
public sealed class PostingReversalServiceTests
{
    private static readonly Guid OwnerId = new("11111111-0000-0000-0000-000000000001");
    private static readonly Guid OtherId = new("22222222-0000-0000-0000-000000000002");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static PostingReversalService CreateService(AppDbContext context, IPostingAggregateService? aggregateService = null)
    {
        var aggService = aggregateService ?? Mock.Of<IPostingAggregateService>();
        return new PostingReversalService(context, aggService, NullLogger<PostingReversalService>.Instance);
    }

    private static Account CreateAccount(Guid ownerId)
    {
        return new Account(ownerId, AccountType.Giro, "Test Account", null, Guid.NewGuid());
    }

    private static Posting CreatePosting(Guid accountId, decimal amount = 100m, string subject = "Test")
    {
        var posting = new Posting(
            sourceId: Guid.NewGuid(),
            kind: PostingKind.Bank,
            accountId: accountId,
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
    /// Happy path: a single posting owned by the user is reversed successfully.
    /// Verifies that a new reversal posting is created with negated amount and
    /// the original is marked as reversed.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldCreateReversalPosting_WhenCalledByOwner()
    {
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var posting = CreatePosting(account.Id, 250m, "Salary");
        context.Postings.Add(posting);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.ReversePostingAsync(posting.Id, OwnerId);

        result.ReversedPostingIds.Should().Contain(posting.Id);
        result.CreatedReversalIds.Should().HaveCount(1);
        var reversalId = result.CreatedReversalIds.Single();
        var reversal = await context.Postings.FindAsync(reversalId);
        reversal.Should().NotBeNull();
        reversal!.Amount.Should().Be(-250m);
        reversal.Subject.Should().StartWith("REVERSAL:");
        reversal.ReversalForPostingId.Should().Be(posting.Id);

        var updated = await context.Postings.FindAsync(posting.Id);
        updated!.ReversedByPostingId.Should().Be(reversalId);
        updated.ReversedByUserId.Should().Be(OwnerId);
    }

    /// <summary>
    /// Group reversal: all postings sharing the same GroupId are reversed together (all-or-nothing).
    /// Verifies that two postings in a group both get reversed.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldReverseEntireGroup_WhenPostingBelongsToGroup()
    {
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);

        var groupId = Guid.NewGuid();
        var posting1 = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 2, 1), 100m, "GroupA", null, null, null);
        posting1.SetGroup(groupId);
        var posting2 = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 2, 1), -100m, "GroupB", null, null, null);
        posting2.SetGroup(groupId);
        context.Postings.AddRange(posting1, posting2);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.ReversePostingAsync(posting1.Id, OwnerId);

        result.ReversedPostingIds.Should().HaveCount(2);
        result.CreatedReversalIds.Should().HaveCount(2);

        var updated1 = await context.Postings.FindAsync(posting1.Id);
        var updated2 = await context.Postings.FindAsync(posting2.Id);
        updated1!.IsReversed.Should().BeTrue();
        updated2!.IsReversed.Should().BeTrue();
    }

    /// <summary>
    /// Already reversed: reversing a posting that has already been reversed must throw.
    /// Verifies the all-or-nothing guard prevents double-reversal.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldThrow_WhenPostingAlreadyReversed()
    {
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var posting = CreatePosting(account.Id, 50m);
        context.Postings.Add(posting);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.ReversePostingAsync(posting.Id, OwnerId);

        var act = async () => await service.ReversePostingAsync(posting.Id, OwnerId);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Unauthorized user: a user who does not own the posting's account must not be able to reverse it.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldThrow_WhenUserIsNotOwner()
    {
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var posting = CreatePosting(account.Id, 75m);
        context.Postings.Add(posting);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var act = async () => await service.ReversePostingAsync(posting.Id, OtherId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{OtherId}*");
    }

    /// <summary>
    /// Reversal of reversal: a posting that is itself a reversal (IsReversal = true) must not be reversed again.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldThrow_WhenPostingIsReversal()
    {
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);

        var original = CreatePosting(account.Id, 100m, "Original");
        var reversal = CreatePosting(account.Id, -100m, "REVERSAL: Original");
        reversal.SetReversalFor(original);
        context.Postings.AddRange(original, reversal);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var act = async () => await service.ReversePostingAsync(reversal.Id, OwnerId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reversal*");
    }

    /// <summary>
    /// Partially reversed group: if any member of the group is already reversed, the whole reversal must fail.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldThrow_WhenGroupIsPartiallyReversed()
    {
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);

        var groupId = Guid.NewGuid();
        var posting1 = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 3, 1), 100m, "P1", null, null, null);
        posting1.SetGroup(groupId);
        var posting2 = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 3, 1), -100m, "P2", null, null, null);
        posting2.SetGroup(groupId);
        // Mark posting2 as already reversed
        var dummyReversal = CreatePosting(account.Id, 100m, "REVERSAL: P2");
        dummyReversal.SetReversalFor(posting2);
        posting2.SetReversedBy(dummyReversal, OwnerId);

        context.Postings.AddRange(posting1, posting2, dummyReversal);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var act = async () => await service.ReversePostingAsync(posting1.Id, OwnerId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*partially reversed*");
    }

    /// <summary>
    /// Not found: reversing a non-existent posting ID must fail validation.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldThrow_WhenPostingNotFound()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var nonExistentId = Guid.NewGuid();
        var act = async () => await service.ReversePostingAsync(nonExistentId, OwnerId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{nonExistentId}*");
    }

    /// <summary>
    /// CanReverseAsync returns valid for an owner with a reversible posting.
    /// </summary>
    [Fact]
    public async Task CanReverseAsync_ShouldReturnValid_WhenOwnerAndReversible()
    {
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var posting = CreatePosting(account.Id, 200m);
        context.Postings.Add(posting);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var validation = await service.CanReverseAsync(posting.Id, OwnerId);

        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// GetRelatedPostingsAsync returns all group members except the queried posting itself.
    /// Postings in different groups must not be included.
    /// </summary>
    [Fact]
    public async Task GetRelatedPostingsAsync_ShouldReturnGroupMembers_ExcludingQueriedPosting()
    {
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);

        var groupId = Guid.NewGuid();
        var posting1 = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 4, 1), 100m, "G1", null, null, null);
        posting1.SetGroup(groupId);
        var posting2 = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 4, 1), -100m, "G2", null, null, null);
        posting2.SetGroup(groupId);
        var posting3 = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 4, 1), 50m, "Other", null, null, null);
        posting3.SetGroup(Guid.NewGuid()); // different group
        context.Postings.AddRange(posting1, posting2, posting3);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var related = await service.GetRelatedPostingsAsync(posting1.Id);

        related.Should().HaveCount(1);
        related.Single().Id.Should().Be(posting2.Id);
    }

    // ─── L07–L21: Additional service tests ───────────────────────────────────

    /// <summary>
    /// L07 – CanReverseAsync returns invalid (not found) for a posting that does not exist.
    /// </summary>
    [Fact]
    public async Task CanReverseAsync_ShouldReturnInvalid_WhenPostingNotFound()
    {
        // Arrange
        await using var context = CreateContext();
        var service = CreateService(context);
        var nonExistentId = new Guid("99999999-0000-0000-0000-000000000001");

        // Act
        var result = await service.CanReverseAsync(nonExistentId, OwnerId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch($"*{nonExistentId}*");
    }

    /// <summary>
    /// L08 – CanReverseAsync returns invalid (not authorized) when the user does not own the posting's account.
    /// </summary>
    [Fact]
    public async Task CanReverseAsync_ShouldReturnInvalid_WhenUserIsNotOwner()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var posting = CreatePosting(account.Id, 100m, "Owned by OwnerId");
        context.Postings.Add(posting);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.CanReverseAsync(posting.Id, OtherId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch($"*{OtherId}*");
    }

    /// <summary>
    /// L09 – CanReverseAsync returns invalid (already reversed) when the posting has been reversed.
    /// </summary>
    [Fact]
    public async Task CanReverseAsync_ShouldReturnInvalid_WhenAlreadyReversed()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var posting = CreatePosting(account.Id, 100m, "Original");
        context.Postings.Add(posting);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        // Reverse once to mark it as already reversed
        await service.ReversePostingAsync(posting.Id, OwnerId);

        // Act
        var result = await service.CanReverseAsync(posting.Id, OwnerId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*already been reversed*");
    }

    /// <summary>
    /// L10 – CanReverseAsync returns invalid (is reversal) when the posting is itself a reversal.
    /// </summary>
    [Fact]
    public async Task CanReverseAsync_ShouldReturnInvalid_WhenPostingIsReversal()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var original = CreatePosting(account.Id, 100m, "Original");
        var reversalPosting = CreatePosting(account.Id, -100m, "REVERSAL: Original");
        reversalPosting.SetReversalFor(original);
        context.Postings.AddRange(original, reversalPosting);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.CanReverseAsync(reversalPosting.Id, OwnerId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*reversal*");
    }

    /// <summary>
    /// L11 – CanReverseAsync returns invalid (partially reversed) when only some members of the group are reversed.
    /// </summary>
    [Fact]
    public async Task CanReverseAsync_ShouldReturnInvalid_WhenGroupIsPartiallyReversed()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);

        var groupId = Guid.NewGuid();
        var p1 = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 5, 1), 100m, "GP1", null, null, null);
        p1.SetGroup(groupId);
        var p2 = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 5, 1), -100m, "GP2", null, null, null);
        p2.SetGroup(groupId);

        var dummyReversal = CreatePosting(account.Id, 100m, "REVERSAL: GP2");
        dummyReversal.SetReversalFor(p2);
        p2.SetReversedBy(dummyReversal, OwnerId);

        context.Postings.AddRange(p1, p2, dummyReversal);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.CanReverseAsync(p1.Id, OwnerId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*partially reversed*");
    }

    /// <summary>
    /// L12 – ReversePostingAsync creates a reversal posting with negated amount and correct subject prefix.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldCreateReversalWithNegatedAmount()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var posting = CreatePosting(account.Id, 500m, "Salary Payment");
        context.Postings.Add(posting);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.ReversePostingAsync(posting.Id, OwnerId);

        // Assert
        var reversalId = result.CreatedReversalIds.Single();
        var reversal = await context.Postings.FindAsync(reversalId);
        reversal.Should().NotBeNull();
        reversal!.Amount.Should().Be(-500m);
        reversal.Subject.Should().StartWith("REVERSAL:");
    }

    /// <summary>
    /// L13 – ReversePostingAsync marks the original posting with ReversedByPostingId and ReversedByUserId.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldMarkOriginalPostingAsReversed()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var posting = CreatePosting(account.Id, 300m, "Test");
        context.Postings.Add(posting);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.ReversePostingAsync(posting.Id, OwnerId);

        // Assert
        var updated = await context.Postings.FindAsync(posting.Id);
        updated!.IsReversed.Should().BeTrue();
        updated.ReversedByPostingId.Should().Be(result.CreatedReversalIds.Single());
        updated.ReversedByUserId.Should().Be(OwnerId);
    }

    /// <summary>
    /// L14 – ReversePostingAsync links the new reversal posting back to the original via ReversalForPostingId.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldSetReversalForPostingId_OnReversalPosting()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var posting = CreatePosting(account.Id, 200m, "Original");
        context.Postings.Add(posting);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.ReversePostingAsync(posting.Id, OwnerId);

        // Assert
        var reversalId = result.CreatedReversalIds.Single();
        var reversal = await context.Postings.FindAsync(reversalId);
        reversal!.IsReversal.Should().BeTrue();
        reversal.ReversalForPostingId.Should().Be(posting.Id);
    }

    /// <summary>
    /// L15 – ReversePostingAsync creates a StatementImport record for the reversal.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldCreateStatementImport()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var posting = CreatePosting(account.Id, 100m, "Test");
        context.Postings.Add(posting);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.ReversePostingAsync(posting.Id, OwnerId);

        // Assert
        result.StatementImportId.Should().NotBe(Guid.Empty);
        var import = await context.StatementImports.FindAsync(result.StatementImportId);
        import.Should().NotBeNull();
    }

    /// <summary>
    /// L15b – The StatementEntry created during reversal reflects the original posting (same amount, same subject).
    /// The entry must NOT be negated and must NOT carry the "REVERSAL:" prefix.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_StatementEntry_ShouldMirrorOriginalPosting()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var posting = CreatePosting(account.Id, 100m, "Gehalt Januar");
        context.Postings.Add(posting);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.ReversePostingAsync(posting.Id, OwnerId);

        // Assert – StatementEntry must mirror the original, not the counter-booking
        var entry = await context.StatementEntries
            .FirstOrDefaultAsync(e => e.StatementImportId == result.StatementImportId);
        entry.Should().NotBeNull();
        entry!.Amount.Should().Be(100m, "the entry amount must equal the original posting amount");
        entry.Subject.Should().Be("Gehalt Januar", "the entry subject must equal the original posting subject without any prefix");
    }

    /// <summary>
    /// L16 – ReversePostingAsync result contains the IDs of all reversed original postings.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldReturnReversedPostingIds_InResult()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var posting = CreatePosting(account.Id, 150m, "ToReverse");
        context.Postings.Add(posting);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.ReversePostingAsync(posting.Id, OwnerId);

        // Assert
        result.ReversedPostingIds.Should().ContainSingle().Which.Should().Be(posting.Id);
        result.CreatedReversalIds.Should().ContainSingle();
    }

    /// <summary>
    /// L17 – When the posting has no AccountId, ownership cannot be determined and
    /// CanReverseAsync throws InvalidOperationException with a message referencing the missing account.
    /// </summary>
    [Fact]
    public async Task CanReverseAsync_ShouldThrow_WhenPostingHasNoAccount()
    {
        // Arrange
        await using var context = CreateContext();
        // Posting without accountId – simulate by using null accountId in the constructor
        var posting = new Posting(
            sourceId: Guid.NewGuid(),
            kind: PostingKind.Bank,
            accountId: null,
            contactId: null,
            savingsPlanId: null,
            securityId: null,
            bookingDate: new DateTime(2025, 6, 1),
            amount: 100m,
            subject: "NoAccount",
            recipientName: null,
            description: null,
            securitySubType: null);
        context.Postings.Add(posting);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var act = async () => await service.CanReverseAsync(posting.Id, OwnerId);

        // Assert – ownership determination throws when AccountId is null
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*account*");
    }

    /// <summary>
    /// L18 – ReversePostingAsync for a group assigns a new shared GroupId to all reversal postings.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldAssignSharedGroupId_ToAllReversalPostings()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);

        var groupId = Guid.NewGuid();
        var p1 = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 7, 1), 100m, "G1", null, null, null);
        p1.SetGroup(groupId);
        var p2 = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 7, 1), -100m, "G2", null, null, null);
        p2.SetGroup(groupId);
        context.Postings.AddRange(p1, p2);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.ReversePostingAsync(p1.Id, OwnerId);

        // Assert
        result.CreatedReversalIds.Should().HaveCount(2);
        var reversals = await context.Postings
            .Where(p => result.CreatedReversalIds.Contains(p.Id))
            .ToListAsync();
        reversals.Should().AllSatisfy(r => r.GroupId.Should().NotBe(Guid.Empty));
        reversals.Select(r => r.GroupId).Distinct().Should().ContainSingle("all reversals share one GroupId");
    }

    /// <summary>
    /// L19 – ReversePostingAsync books the reversal into a new StatementImport in the same account.
    /// The created StatementImport must reference the account of the original posting.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldBookReversalIntoSameAccount()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);
        var posting = CreatePosting(account.Id, 250m, "AccountCheck");
        context.Postings.Add(posting);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.ReversePostingAsync(posting.Id, OwnerId);

        // Assert
        var reversalId = result.CreatedReversalIds.Single();
        var reversal = await context.Postings.FindAsync(reversalId);
        reversal!.AccountId.Should().Be(account.Id);
    }

    /// <summary>
    /// L20 – ReversePostingAsync on a solo posting (no GroupId) reverses exactly one posting.
    /// A posting created without SetGroup() is treated as ungrouped.
    /// </summary>
    [Fact]
    public async Task ReversePostingAsync_ShouldReverseExactlyOnePosting_WhenPostingHasNoGroup()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);

        // Create a posting WITHOUT calling SetGroup – ungrouped
        var posting = new Posting(
            sourceId: Guid.NewGuid(),
            kind: PostingKind.Bank,
            accountId: account.Id,
            contactId: null,
            savingsPlanId: null,
            securityId: null,
            bookingDate: new DateTime(2025, 8, 1),
            amount: 75m,
            subject: "SoloPosing",
            recipientName: null,
            description: null,
            securitySubType: null);
        context.Postings.Add(posting);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.ReversePostingAsync(posting.Id, OwnerId);

        // Assert
        result.ReversedPostingIds.Should().ContainSingle();
        result.CreatedReversalIds.Should().ContainSingle();
    }

    /// <summary>
    /// L21 – GetRelatedPostingsAsync with GroupId == Guid.Empty must return an empty list,
    /// because a posting with no GroupId has no related postings.
    /// </summary>
    [Fact]
    public async Task GetRelatedPostingsAsync_ShouldReturnEmpty_WhenPostingHasGroupIdEmpty()
    {
        // Arrange
        await using var context = CreateContext();
        var account = CreateAccount(OwnerId);
        context.Accounts.Add(account);

        // A posting whose GroupId is Guid.Empty (ungrouped, never had SetGroup called)
        var posting = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 9, 1), 50m, "Solo", null, null, null);
        // A second ungrouped posting – the bug causes it to be returned as a "related" posting
        var unrelated = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 9, 1), 60m, "Unrelated", null, null, null);
        context.Postings.AddRange(posting, unrelated);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var related = await service.GetRelatedPostingsAsync(posting.Id);

        // Assert – currently fails because the query returns `unrelated`
        related.Should().BeEmpty("a posting with no GroupId should have no related postings");
    }
}
