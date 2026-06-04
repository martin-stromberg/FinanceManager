using FinanceManager.Application.Budget;
using FinanceManager.Domain.Budget;
using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Statements;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Infrastructure.Statements;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Statements;

public sealed class BudgetImpactEvaluationServiceTests
{
    [Fact]
    public async Task EvaluateEntryImpactAsync_ShouldReturnExceededHint_WhenBookingExceedsTarget()
    {
        var ownerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var user = new User("impact-user", "hash");
        TestEntityHelper.SetEntityId(user, ownerId);
        db.Users.Add(user);

        var contact = new Contact(ownerId, "Groceries", ContactType.Other, null);
        TestEntityHelper.SetEntityId(contact, contactId);
        db.Contacts.Add(contact);

        var purpose = new BudgetPurpose(ownerId, "Groceries Budget", BudgetSourceType.Contact, contactId);
        db.BudgetPurposes.Add(purpose);
        db.BudgetRules.Add(new BudgetRule(ownerId, purpose.Id, null, 100m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1)));

        var draft = new StatementDraft(ownerId, "draft.csv", null, null);
        var entry = draft.AddEntry(new DateTime(2026, 5, 12), 40m, "Purchase");
        entry.MarkAccounted(contactId);
        db.StatementDrafts.Add(draft);

        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Contact, null, contactId, null, null, new DateTime(2026, 5, 5), 80m));
        await db.SaveChangesAsync();

        var planningRepo = new BudgetPlanningRepository(db);
        var planning = new BudgetPlanningService(NullLogger<BudgetPlanningService>.Instance, planningRepo);
        var sut = new BudgetImpactEvaluationService(db, planning, NullLogger<BudgetImpactEvaluationService>.Instance);

        var result = await sut.EvaluateEntryImpactAsync(draft.Id, entry.Id, ownerId, CancellationToken.None);

        Assert.NotNull(result);
        var hint = Assert.Single(result!.Hints.Where(x => x.BudgetPurposeId == purpose.Id));
        Assert.Equal(BudgetImpactHintType.Exceeded, hint.HintType);
        Assert.Equal(100m, hint.TargetValue);
        Assert.Equal(80m, hint.ActualBefore);
        Assert.Equal(120m, hint.ActualAfter);
    }

    [Fact]
    public async Task EvaluateDraftImpactAsync_ShouldReturnNeutralItem_WhenNoBudgetPurposeMatches()
    {
        var ownerId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var user = new User("impact-user-2", "hash");
        TestEntityHelper.SetEntityId(user, ownerId);
        db.Users.Add(user);

        var draft = new StatementDraft(ownerId, "draft.csv", null, null);
        draft.AddEntry(new DateTime(2026, 6, 2), 55m, "Unassigned booking");
        db.StatementDrafts.Add(draft);
        await db.SaveChangesAsync();

        var planningRepo = new BudgetPlanningRepository(db);
        var planning = new BudgetPlanningService(NullLogger<BudgetPlanningService>.Instance, planningRepo);
        var sut = new BudgetImpactEvaluationService(db, planning, NullLogger<BudgetImpactEvaluationService>.Instance);

        var result = await sut.EvaluateDraftImpactAsync(draft.Id, null, ownerId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(BudgetImpactHintType.Neutral, result!.HighestSeverity);
        Assert.Single(result.Items);
        Assert.Equal(BudgetImpactHintType.Neutral, result.Items[0].HintType);
    }
}
