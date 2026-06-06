using FinanceManager.Application;
using FinanceManager.Application.Budget;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Controllers;

/// <summary>
/// Verifies BudgetRulesController validation behavior around regex patterns.
/// </summary>
public sealed class BudgetRulesControllerTests
{
    /// <summary>
    /// Ensures create maps invalid regex errors to a validation problem response.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldReturnValidationProblem_WhenRegexPatternIsInvalid()
    {
        var service = new Mock<IBudgetRuleService>();
        service
            .Setup(x => x.CreateAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<decimal>(),
                It.IsAny<BudgetIntervalType>(),
                It.IsAny<int?>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid regex", "pattern"));

        var controller = CreateController(service.Object);
        var request = new BudgetRuleCreateRequest(
            BudgetPurposeId: Guid.NewGuid(),
            BudgetCategoryId: null,
            Amount: 100m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null,
            PurposePattern: "(",
            UseRegex: true);

        var result = await controller.CreateAsync(request, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        var details = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey(nameof(BudgetRuleCreateRequest.PurposePattern));
    }

    /// <summary>
    /// Ensures update maps invalid regex errors to a validation problem response.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldReturnValidationProblem_WhenRegexPatternIsInvalid()
    {
        var service = new Mock<IBudgetRuleService>();
        service
            .Setup(x => x.UpdateAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<decimal>(),
                It.IsAny<BudgetIntervalType>(),
                It.IsAny<int?>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid regex", "pattern"));

        var controller = CreateController(service.Object);
        var request = new BudgetRuleUpdateRequest(
            Amount: 100m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null,
            PurposePattern: "(",
            UseRegex: true);

        var result = await controller.UpdateAsync(Guid.NewGuid(), request, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        var details = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey(nameof(BudgetRuleUpdateRequest.PurposePattern));
    }

    /// <summary>
    /// Ensures update accepts null pattern (clearing the pattern is allowed).
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldAcceptNullPattern_WhenPatternIsCleared()
    {
        var ruleId = Guid.NewGuid();
        var cleared = new BudgetRuleDto(
            Id: ruleId,
            OwnerUserId: Guid.NewGuid(),
            BudgetPurposeId: Guid.NewGuid(),
            BudgetCategoryId: null,
            Amount: 100m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null,
            PurposePattern: null,  // <- Cleared
            UseRegex: false);

        var service = new Mock<IBudgetRuleService>();
        service
            .Setup(x => x.UpdateAsync(
                ruleId,
                It.IsAny<Guid>(),  // <- UserId from ICurrentUserService
                100m,
                BudgetIntervalType.Monthly,
                null,
                new DateOnly(2026, 1, 1),
                null,
                null,  // <- Null pattern
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cleared);

        var controller = CreateController(service.Object);
        var request = new BudgetRuleUpdateRequest(
            Amount: 100m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null,
            PurposePattern: null,  // <- Clear pattern
            UseRegex: false);

        var result = await controller.UpdateAsync(ruleId, request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        service.Verify(x => x.UpdateAsync(
            ruleId,
            It.IsAny<Guid>(),
            It.IsAny<decimal>(),
            It.IsAny<BudgetIntervalType>(),
            It.IsAny<int?>(),
            It.IsAny<DateOnly>(),
            It.IsAny<DateOnly?>(),
            null,  // <- Verify null was passed
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static BudgetRulesController CreateController(IBudgetRuleService service)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        var provider = services.BuildServiceProvider();
        var localizer = provider.GetRequiredService<IStringLocalizer<FinanceManager.Web.Controllers.Controller>>();

        return new BudgetRulesController(
            service,
            new TestCurrentUserService(),
            NullLogger<BudgetRulesController>.Instance,
            localizer);
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public bool IsAuthenticated => true;

        public bool IsAdmin => false;

        public string? PreferredLanguage => null;
    }
}
