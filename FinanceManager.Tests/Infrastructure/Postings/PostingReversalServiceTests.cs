using FinanceManager.Application.Aggregates;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
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
}
