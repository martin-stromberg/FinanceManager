using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Xunit;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Application.Budget;
using FinanceManager.Infrastructure.Budget;
using System.Threading;
using FinanceManager.Infrastructure;
using FinanceManager.Domain.Postings;

namespace FinanceManager.Tests.Budget;

public sealed class BudgetReportServiceKpiExpectedValuesTests
{
    [Fact]
    public async Task GetMonthlyKpiAsync_ShouldReturn_ExpectedIncomeAndExpense()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(dbContextOptions);

        // Budgetierte Einnahmen: 2000, 1000
        var contactIncome1 = Guid.NewGuid();
        var contactIncome2 = Guid.NewGuid();
        // Budgetierte Ausgaben: -1000, -500, -500
        var contactExpense1 = Guid.NewGuid();
        var contactExpense2 = Guid.NewGuid();
        var contactExpense3 = Guid.NewGuid();
        // Unbudgetiert: -200 (Expense), +50 (Income)
        var contactUnbudgetedExpense = Guid.NewGuid();
        var contactUnbudgetedIncome = Guid.NewGuid();

        // Purposes
        var purposes = new List<BudgetPurposeOverviewDto>
        {
            new BudgetPurposeOverviewDto(
                Guid.NewGuid(), userId, "Einnahme1", null,
                FinanceManager.Shared.Dtos.Budget.BudgetSourceType.Contact, contactIncome1, 0,
                2000m, 2000m, 0m, null, null, null, null),
            new BudgetPurposeOverviewDto(
                Guid.NewGuid(), userId, "Einnahme2", null,
                FinanceManager.Shared.Dtos.Budget.BudgetSourceType.Contact, contactIncome2, 0,
                1000m, 0m, -1000m, null, null, null, null),
            new BudgetPurposeOverviewDto(
                Guid.NewGuid(), userId, "Ausgabe1", null,
                FinanceManager.Shared.Dtos.Budget.BudgetSourceType.Contact, contactExpense1, 0,
                -1000m, -1000m, 0m, null, null, null, null),
            new BudgetPurposeOverviewDto(
                Guid.NewGuid(), userId, "Ausgabe2", null,
                FinanceManager.Shared.Dtos.Budget.BudgetSourceType.Contact, contactExpense2, 0,
                -500m, 0m, 500m, null, null, null, null),
            new BudgetPurposeOverviewDto(
                Guid.NewGuid(), userId, "Ausgabe3", null,
                FinanceManager.Shared.Dtos.Budget.BudgetSourceType.Contact, contactExpense3, 0,
                -500m, 0m, 500m, null, null, null, null),
        };

        // Postings (Ist-Buchungen) as domain Posting instances
        db.Postings.AddRange(
            new Posting(Guid.NewGuid(), PostingKind.Contact, null, contactIncome1, null, null, DateTime.UtcNow.Date, 2000m),
            new Posting(Guid.NewGuid(), PostingKind.Contact, null, contactExpense1, null, null, DateTime.UtcNow.Date, -1000m),
            new Posting(Guid.NewGuid(), PostingKind.Contact, null, contactUnbudgetedExpense, null, null, DateTime.UtcNow.Date, -200m),
            new Posting(Guid.NewGuid(), PostingKind.Contact, null, contactUnbudgetedIncome, null, null, DateTime.UtcNow.Date, 50m)
        );
        await db.SaveChangesAsync();

        var mockPurposeService = new TestPurposeService(purposes);
        var mockCategoryService = new TestCategoryService();
        var service = new BudgetReportService(mockPurposeService, mockCategoryService, db);

        // Act
        var result = await service.GetMonthlyKpiAsync(userId, CancellationToken.None);

        // Assert
        result.PlannedIncome.Should().Be(3000);
        result.PlannedExpenseAbs.Should().Be(2000);
        result.ActualIncome.Should().Be(2000 + 50);
        result.ActualExpenseAbs.Should().Be(1000 + 200);
        result.ExpectedIncome.Should().Be(3050);
        result.ExpectedExpenseAbs.Should().Be(2200);
    }

    // TestPurposeService und TestCategoryService als einfache Mocks
    private class TestPurposeService : IBudgetPurposeService
    {
        private readonly IReadOnlyList<BudgetPurposeOverviewDto> _purposes;
        public TestPurposeService(IReadOnlyList<BudgetPurposeOverviewDto> purposes) => _purposes = purposes;

        public Task<FinanceManager.Shared.Dtos.Budget.BudgetPurposeDto> CreateAsync(Guid ownerUserId, string name, BudgetSourceType sourceType, Guid sourceId, string? description, Guid? budgetCategoryId, CancellationToken ct) => throw new NotImplementedException();
        public Task<FinanceManager.Shared.Dtos.Budget.BudgetPurposeDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, BudgetSourceType sourceType, Guid sourceId, string? description, Guid? budgetCategoryId, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<FinanceManager.Shared.Dtos.Budget.BudgetPurposeDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<FinanceManager.Shared.Dtos.Budget.BudgetPurposeDto>> ListAsync(Guid ownerUserId, int skip, int take, BudgetSourceType? sourceType, string? nameFilter, CancellationToken ct) => throw new NotImplementedException();
        public Task<int> CountAsync(Guid ownerUserId, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<BudgetPurposeOverviewDto>> ListOverviewAsync(Guid ownerUserId, int skip, int take, BudgetSourceType? sourceType, string? nameFilter, DateOnly? from, DateOnly? to, Guid? budgetCategoryId, CancellationToken ct, BudgetReportDateBasis dateBasis = BudgetReportDateBasis.BookingDate)
            => Task.FromResult(_purposes);
    }

    private class TestCategoryService : IBudgetCategoryService
    {
        public Task<IReadOnlyList<BudgetCategoryDto>> ListAsync(Guid ownerUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<BudgetCategoryDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<BudgetCategoryDto> CreateAsync(Guid ownerUserId, string name, CancellationToken ct) => throw new NotImplementedException();
        public Task<BudgetCategoryDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<BudgetCategoryOverviewDto>> ListOverviewAsync(Guid ownerUserId, DateOnly? from, DateOnly? to, CancellationToken ct) => Task.FromResult((IReadOnlyList<BudgetCategoryOverviewDto>)new List<BudgetCategoryOverviewDto>());
    }
}
