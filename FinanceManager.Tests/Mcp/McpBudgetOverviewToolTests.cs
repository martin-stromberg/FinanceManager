using FinanceManager.Application.Budget;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Web.Mcp.Tools;
using Moq;
using System.Text.Json;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Tests of the <c>fm_budget_overview</c> handler output: the inherited category-level budget
/// (<c>parentBudget</c>) must be surfaced per purpose so purposes without own rules are not
/// mistaken for unbudgeted (the category carries the plan).
/// </summary>
public sealed class McpBudgetOverviewToolTests
{
    private static McpAccess NewAccess(Guid owner)
        => new(owner, "acc", "HINT01", "pbkdf2|1|AA|AA", McpAccessLevel.Analyse, McpApprovalMode.PerAction);

    private static McpToolCallContext NewCallContext(Guid owner)
    {
        var ctx = new Mock<IMcpAccessContext>();
        ctx.SetupGet(c => c.OwnerUserId).Returns(owner);
        ctx.SetupGet(c => c.Access).Returns(NewAccess(owner));
        return new McpToolCallContext(ctx.Object, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null);
    }

    /// <summary>
    /// A purpose bound to a budget category must carry the category's rules in <c>parentBudget</c>,
    /// even when the purpose itself has no own rules.
    /// </summary>
    [Fact]
    public async Task Purpose_WithCategoryRule_ShowsParentBudget()
    {
        var owner = Guid.NewGuid();
        var catId = Guid.NewGuid();
        var purposeId = Guid.NewGuid();

        var purposes = new Mock<IBudgetPurposeService>();
        purposes.Setup(p => p.ListOverviewAsync(
                owner, 0, It.IsAny<int>(), null, null, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), null,
                It.IsAny<CancellationToken>(), It.IsAny<BudgetReportDateBasis>()))
            .ReturnsAsync(new List<BudgetPurposeOverviewDto>
            {
                new(purposeId, owner, "Supermärkte & Einzelhandel", null, BudgetSourceType.ContactGroup,
                    Guid.NewGuid(), 0, 0m, -471.79m, -471.79m, "Supermärkte & Einzelhandel", null,
                    catId, "Einkaufen & Verpflegung")
            });

        var rules = new Mock<IBudgetRuleService>();
        rules.Setup(r => r.ListByPurposeAsync(owner, purposeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetRuleDto>());
        rules.Setup(r => r.ListByCategoryAsync(owner, catId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetRuleDto>
            {
                new(Guid.NewGuid(), owner, null, catId, -500m, BudgetIntervalType.Monthly, null, new DateOnly(2020, 1, 1), null)
            });

        var overrides = new Mock<IBudgetOverrideService>();
        overrides.Setup(o => o.ListByPurposeAsync(owner, purposeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetOverrideDto>());

        var categories = new Mock<IBudgetCategoryService>();
        categories.Setup(c => c.ListAsync(owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetCategoryDto>());

        var handler = new FmBudgetOverviewHandler(purposes.Object, rules.Object, overrides.Object, categories.Object);
        var result = await handler.InvokeAsync(
            JsonDocument.Parse("{}").RootElement, NewCallContext(owner), TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var purpose = doc.RootElement.GetProperty("purposes")[0];
        var parent = purpose.GetProperty("parentBudget");

        Assert.NotEqual(JsonValueKind.Null, parent.ValueKind);
        Assert.Equal(catId.ToString(), parent.GetProperty("budgetCategoryId").GetString());
        var catRules = parent.GetProperty("rules");
        Assert.Equal(1, catRules.GetArrayLength());
        Assert.Equal(-500m, catRules[0].GetProperty("amount").GetDecimal());
        Assert.Equal("Monthly", catRules[0].GetProperty("interval").GetString());
    }

    /// <summary>
    /// A purpose bound to a budget category without any category rule must report
    /// <c>parentBudget</c> with an empty rule list — never silently dropped.
    /// </summary>
    [Fact]
    public async Task Purpose_WithCategoryWithoutRules_ShowsEmptyParentBudget()
    {
        var owner = Guid.NewGuid();
        var catId = Guid.NewGuid();
        var purposeId = Guid.NewGuid();

        var purposes = new Mock<IBudgetPurposeService>();
        purposes.Setup(p => p.ListOverviewAsync(
                owner, 0, It.IsAny<int>(), null, null, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), null,
                It.IsAny<CancellationToken>(), It.IsAny<BudgetReportDateBasis>()))
            .ReturnsAsync(new List<BudgetPurposeOverviewDto>
            {
                new(purposeId, owner, "KFZ Werkstatt", null, BudgetSourceType.Contact,
                    Guid.NewGuid(), 0, 0m, -56.64m, -56.64m, "Autohaus", null, catId, "Mobilität")
            });

        var rules = new Mock<IBudgetRuleService>();
        rules.Setup(r => r.ListByPurposeAsync(owner, purposeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetRuleDto>());
        rules.Setup(r => r.ListByCategoryAsync(owner, catId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetRuleDto>());

        var overrides = new Mock<IBudgetOverrideService>();
        overrides.Setup(o => o.ListByPurposeAsync(owner, purposeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetOverrideDto>());

        var categories = new Mock<IBudgetCategoryService>();
        categories.Setup(c => c.ListAsync(owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetCategoryDto>());

        var handler = new FmBudgetOverviewHandler(purposes.Object, rules.Object, overrides.Object, categories.Object);
        var result = await handler.InvokeAsync(
            JsonDocument.Parse("{}").RootElement, NewCallContext(owner), TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var purpose = doc.RootElement.GetProperty("purposes")[0];
        var parent = purpose.GetProperty("parentBudget");

        Assert.NotEqual(JsonValueKind.Null, parent.ValueKind);
        Assert.Equal(0, parent.GetProperty("rules").GetArrayLength());
    }
}
