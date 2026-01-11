using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

public sealed class ApiClientBudgetsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ApiClientBudgetsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new FinanceManager.Shared.ApiClient(http);
    }

    private static async Task EnsureAuthenticatedAsync(FinanceManager.Shared.ApiClient api)
    {
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
    }

    [Fact]
    public async Task Budgets_Purposes_Rules_Overrides_Flow()
    {
        // Arrange
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        // Act + Assert: purposes empty
        var purposes0 = await api.Budgets_ListPurposesAsync();
        purposes0.Should().NotBeNull();
        purposes0.Should().BeEmpty();

        BudgetPurposeDto createdPurpose;
        try
        {
            // Create purpose
            createdPurpose = await api.Budgets_CreatePurposeAsync(new BudgetPurposeCreateRequest(
                Name: "Groceries",
                SourceType: BudgetSourceType.ContactGroup,
                SourceId: Guid.NewGuid(),
                Description: null));
        }
        catch (HttpRequestException)
        {
            throw new Xunit.Sdk.XunitException($"Budget purpose create failed. LastError: '{api.LastError}', LastErrorCode: '{api.LastErrorCode}'");
        }

        createdPurpose.Id.Should().NotBeEmpty();
        createdPurpose.Name.Should().Be("Groceries");

        // Get purpose
        var gotPurpose = await api.Budgets_GetPurposeAsync(createdPurpose.Id);
        gotPurpose.Should().NotBeNull();
        gotPurpose!.Id.Should().Be(createdPurpose.Id);

        // Update purpose
        var updatedPurpose = await api.Budgets_UpdatePurposeAsync(createdPurpose.Id, new BudgetPurposeUpdateRequest(
            Name: "Groceries2",
            SourceType: BudgetSourceType.ContactGroup,
            SourceId: createdPurpose.SourceId,
            Description: "desc"));

        updatedPurpose.Should().NotBeNull();
        updatedPurpose!.Name.Should().Be("Groceries2");
        updatedPurpose.Description.Should().Be("desc");

        // Create rules (one yearly in January, one monthly)
        var ruleYearlyJan = await api.Budgets_CreateRuleAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: createdPurpose.Id,
            Amount: 90m,
            Interval: BudgetIntervalType.Yearly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null));

        var ruleMonthly = await api.Budgets_CreateRuleAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: createdPurpose.Id,
            Amount: 10m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null));

        // Create a monthly rule that ends after February 2026
        var ruleMonthlyEndsFeb = await api.Budgets_CreateRuleAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: createdPurpose.Id,
            Amount: 5m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: new DateOnly(2026, 2, 28)));

        ruleYearlyJan.Id.Should().NotBeEmpty();
        ruleMonthly.Id.Should().NotBeEmpty();
        ruleMonthlyEndsFeb.Id.Should().NotBeEmpty();

        // List rules
        var rules = await api.Budgets_ListRulesByPurposeAsync(createdPurpose.Id);
        rules.Should().HaveCount(3);

        // Purposes overview with range filter: February 2026 should include monthly occurrence (10) + ending rule (5)
        var febPurposes = await api.Budgets_ListPurposesAsync(
            skip: 0,
            take: 200,
            sourceType: null,
            q: null,
            from: new DateOnly(2026, 2, 1),
            to: new DateOnly(2026, 2, 28));

        febPurposes.Should().HaveCount(1);
        febPurposes[0].Id.Should().Be(createdPurpose.Id);
        febPurposes[0].RuleCount.Should().Be(3);
        febPurposes[0].BudgetSum.Should().Be(15m);
        febPurposes[0].ActualSum.Should().Be(0m);
        febPurposes[0].Variance.Should().Be(-15m);
        febPurposes[0].SourceName.Should().NotBeNull();

        // Purposes overview with range filter: January 2026 should include yearly + monthly + ending rule (90 + 10 + 5)
        var janPurposes = await api.Budgets_ListPurposesAsync(
            skip: 0,
            take: 200,
            sourceType: null,
            q: null,
            from: new DateOnly(2026, 1, 1),
            to: new DateOnly(2026, 1, 31));

        janPurposes.Should().HaveCount(1);
        janPurposes[0].Id.Should().Be(createdPurpose.Id);
        janPurposes[0].RuleCount.Should().Be(3);
        janPurposes[0].BudgetSum.Should().Be(105m);
        janPurposes[0].ActualSum.Should().Be(0m);
        janPurposes[0].Variance.Should().Be(-105m);
        janPurposes[0].SourceName.Should().NotBeNull();

        // Purposes overview with range filter: March 2026 should include only the non-ending monthly rule (10)
        var marPurposes = await api.Budgets_ListPurposesAsync(
            skip: 0,
            take: 200,
            sourceType: null,
            q: null,
            from: new DateOnly(2026, 3, 1),
            to: new DateOnly(2026, 3, 31));

        marPurposes.Should().HaveCount(1);
        marPurposes[0].Id.Should().Be(createdPurpose.Id);
        marPurposes[0].RuleCount.Should().Be(3);
        marPurposes[0].BudgetSum.Should().Be(10m);
        marPurposes[0].ActualSum.Should().Be(0m);
        marPurposes[0].Variance.Should().Be(-10m);
        marPurposes[0].SourceName.Should().NotBeNull();

        // Update rule (monthly amount)
        var updatedRule = await api.Budgets_UpdateRuleAsync(ruleMonthly.Id, new BudgetRuleUpdateRequest(
            Amount: 12m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null));

        updatedRule.Should().NotBeNull();
        updatedRule!.Amount.Should().Be(12m);

        // Create override
        var createdOverride = await api.Budgets_CreateOverrideAsync(new BudgetOverrideCreateRequest(
            BudgetPurposeId: createdPurpose.Id,
            Period: new BudgetPeriodKey(2026, 3),
            Amount: 500m));

        createdOverride.Id.Should().NotBeEmpty();

        // List overrides
        var overrides = await api.Budgets_ListOverridesByPurposeAsync(createdPurpose.Id);
        overrides.Should().HaveCount(1);
        overrides[0].Id.Should().Be(createdOverride.Id);

        // Update override
        var updatedOverride = await api.Budgets_UpdateOverrideAsync(createdOverride.Id, new BudgetOverrideUpdateRequest(
            Period: new BudgetPeriodKey(2026, 3),
            Amount: 550m));

        updatedOverride.Should().NotBeNull();
        updatedOverride!.Amount.Should().Be(550m);

        // Delete override
        var delOverrideOk = await api.Budgets_DeleteOverrideAsync(createdOverride.Id);
        delOverrideOk.Should().BeTrue();
        (await api.Budgets_GetOverrideAsync(createdOverride.Id)).Should().BeNull();

        // Delete rules
        var delRuleOk1 = await api.Budgets_DeleteRuleAsync(ruleYearlyJan.Id);
        delRuleOk1.Should().BeTrue();
        (await api.Budgets_GetRuleAsync(ruleYearlyJan.Id)).Should().BeNull();

        var delRuleOk2 = await api.Budgets_DeleteRuleAsync(ruleMonthly.Id);
        delRuleOk2.Should().BeTrue();
        (await api.Budgets_GetRuleAsync(ruleMonthly.Id)).Should().BeNull();

        var delRuleOk3 = await api.Budgets_DeleteRuleAsync(ruleMonthlyEndsFeb.Id);
        delRuleOk3.Should().BeTrue();
        (await api.Budgets_GetRuleAsync(ruleMonthlyEndsFeb.Id)).Should().BeNull();

        // Delete purpose
        var delPurposeOk = await api.Budgets_DeletePurposeAsync(createdPurpose.Id);
        delPurposeOk.Should().BeTrue();
        (await api.Budgets_GetPurposeAsync(createdPurpose.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Budgets_DeletePurpose_ShouldAlsoDeleteRules()
    {
        // Arrange
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var purpose = await api.Budgets_CreatePurposeAsync(new BudgetPurposeCreateRequest(
            Name: "TestPurpose",
            SourceType: BudgetSourceType.ContactGroup,
            SourceId: Guid.NewGuid(),
            Description: null));

        var rule1 = await api.Budgets_CreateRuleAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: purpose.Id,
            Amount: 10m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null));

        var rule2 = await api.Budgets_CreateRuleAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: purpose.Id,
            Amount: 20m,
            Interval: BudgetIntervalType.Yearly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null));

        (await api.Budgets_ListRulesByPurposeAsync(purpose.Id)).Should().HaveCount(2);

        // Act
        var deleted = await api.Budgets_DeletePurposeAsync(purpose.Id);

        // Assert
        deleted.Should().BeTrue();
        (await api.Budgets_GetPurposeAsync(purpose.Id)).Should().BeNull();

        (await api.Budgets_GetRuleAsync(rule1.Id)).Should().BeNull();
        (await api.Budgets_GetRuleAsync(rule2.Id)).Should().BeNull();
    }
}
