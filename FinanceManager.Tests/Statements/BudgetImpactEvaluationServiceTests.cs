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
using FluentAssertions;
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
        Assert.Empty(result.Items);
    }

    /// <summary>
    /// Verifies that regex purpose patterns are respected for statement draft budget impact checks.
    /// </summary>
    [Fact]
    public async Task EvaluateDraftImpactAsync_ShouldReturnImpactItem_WhenRegexPurposePatternMatches()
    {
        var ownerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var user = new User("impact-user-regex", "hash");
        TestEntityHelper.SetEntityId(user, ownerId);
        db.Users.Add(user);

        var contact = new Contact(ownerId, "Utility", ContactType.Other, null);
        TestEntityHelper.SetEntityId(contact, contactId);
        db.Contacts.Add(contact);

        var purpose = new BudgetPurpose(ownerId, "Utility Budget", BudgetSourceType.Contact, contactId);
        db.BudgetPurposes.Add(purpose);
        db.BudgetRules.Add(new BudgetRule(ownerId, purpose.Id, null, 100m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1), null, null, "ST\\d{10}", true));

        var draft = new StatementDraft(ownerId, "draft.csv", null, null);
        var entry = draft.AddEntry(new DateTime(2026, 6, 2), 55m, "Abrechnung ST6464646464 Juni");
        entry.MarkAccounted(contactId);
        db.StatementDrafts.Add(draft);

        await db.SaveChangesAsync();

        var planningRepo = new BudgetPlanningRepository(db);
        var planning = new BudgetPlanningService(NullLogger<BudgetPlanningService>.Instance, planningRepo);
        var sut = new BudgetImpactEvaluationService(db, planning, NullLogger<BudgetImpactEvaluationService>.Instance);

        var result = await sut.EvaluateDraftImpactAsync(draft.Id, null, ownerId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Items.Should().ContainSingle(x => x.BudgetPurposeId == purpose.Id);
    }

    /// <summary>
    /// Verifies that empty purpose text does not match non-empty patterns and stays neutral.
    /// </summary>
    [Fact]
    public async Task EvaluateDraftImpactAsync_ShouldReturnNeutral_WhenPurposeTextIsEmptyAndPatternIsDefined()
    {
        var ownerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var user = new User("impact-user-empty", "hash");
        TestEntityHelper.SetEntityId(user, ownerId);
        db.Users.Add(user);

        var contact = new Contact(ownerId, "Utility", ContactType.Other, null);
        TestEntityHelper.SetEntityId(contact, contactId);
        db.Contacts.Add(contact);

        var purpose = new BudgetPurpose(ownerId, "Utility Budget", BudgetSourceType.Contact, contactId);
        db.BudgetPurposes.Add(purpose);
        db.BudgetRules.Add(new BudgetRule(ownerId, purpose.Id, null, 100m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1), null, null, "ABCD", false));

        var draft = new StatementDraft(ownerId, "draft.csv", null, null);
        var entry = draft.AddEntry(new DateTime(2026, 6, 2), 55m, string.Empty, null, null, null, null, false);
        entry.MarkAccounted(contactId);
        db.StatementDrafts.Add(draft);

        await db.SaveChangesAsync();

        var planningRepo = new BudgetPlanningRepository(db);
        var planning = new BudgetPlanningService(NullLogger<BudgetPlanningService>.Instance, planningRepo);
        var sut = new BudgetImpactEvaluationService(db, planning, NullLogger<BudgetImpactEvaluationService>.Instance);

        var result = await sut.EvaluateDraftImpactAsync(draft.Id, null, ownerId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.HighestSeverity.Should().Be(BudgetImpactHintType.Neutral);
        result.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies timeout-safe regex handling by ensuring no exception escapes and non-matching entries stay neutral.
    /// </summary>
    [Fact]
    public async Task EvaluateDraftImpactAsync_ShouldRemainNeutral_WhenRegexIsPotentiallyCatastrophic()
    {
        var ownerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var user = new User("impact-user-timeout", "hash");
        TestEntityHelper.SetEntityId(user, ownerId);
        db.Users.Add(user);

        var contact = new Contact(ownerId, "Utility", ContactType.Other, null);
        TestEntityHelper.SetEntityId(contact, contactId);
        db.Contacts.Add(contact);

        var purpose = new BudgetPurpose(ownerId, "Utility Budget", BudgetSourceType.Contact, contactId);
        db.BudgetPurposes.Add(purpose);
        db.BudgetRules.Add(new BudgetRule(ownerId, purpose.Id, null, 100m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1), null, null, "^(a+)+$", true));

        var draft = new StatementDraft(ownerId, "draft.csv", null, null);
        var entry = draft.AddEntry(new DateTime(2026, 6, 2), 55m, $"{new string('a', 20000)}!");
        entry.MarkAccounted(contactId);
        db.StatementDrafts.Add(draft);

        await db.SaveChangesAsync();

        var planningRepo = new BudgetPlanningRepository(db);
        var planning = new BudgetPlanningService(NullLogger<BudgetPlanningService>.Instance, planningRepo);
        var sut = new BudgetImpactEvaluationService(db, planning, NullLogger<BudgetImpactEvaluationService>.Instance);

        var act = async () => await sut.EvaluateDraftImpactAsync(draft.Id, null, ownerId, CancellationToken.None);
        var result = await act.Should().NotThrowAsync();

        result.Subject.Should().NotBeNull();
        result.Subject!.Items.Should().BeEmpty();
    }
}
