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

        // Create rule
        var createdRule = await api.Budgets_CreateRuleAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: createdPurpose.Id,
            Amount: 350m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null));

        createdRule.Id.Should().NotBeEmpty();
        createdRule.BudgetPurposeId.Should().Be(createdPurpose.Id);

        // List rules
        var rules = await api.Budgets_ListRulesByPurposeAsync(createdPurpose.Id);
        rules.Should().HaveCount(1);
        rules[0].Id.Should().Be(createdRule.Id);

        // Update rule
        var updatedRule = await api.Budgets_UpdateRuleAsync(createdRule.Id, new BudgetRuleUpdateRequest(
            Amount: 400m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null));

        updatedRule.Should().NotBeNull();
        updatedRule!.Amount.Should().Be(400m);

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

        // Delete rule
        var delRuleOk = await api.Budgets_DeleteRuleAsync(createdRule.Id);
        delRuleOk.Should().BeTrue();
        (await api.Budgets_GetRuleAsync(createdRule.Id)).Should().BeNull();

        // Delete purpose
        var delPurposeOk = await api.Budgets_DeletePurposeAsync(createdPurpose.Id);
        delPurposeOk.Should().BeTrue();
        (await api.Budgets_GetPurposeAsync(createdPurpose.Id)).Should().BeNull();
    }
}
