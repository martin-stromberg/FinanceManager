using Bunit;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Web;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Tests.Components;

public sealed class ValidationResultPanelTests : BunitContext
{
    /// <summary>
    /// Ensures that a general validation message with related-record metadata renders a card link.
    /// </summary>
    [Fact]
    public void GeneralMessage_WithRelatedRecord_RendersCardLink()
    {
        // Arrange
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
        Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

        var draftId = Guid.NewGuid();
        var relatedId = Guid.NewGuid();
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"http://localhost/card/statement-drafts/{draftId}");

        var result = new DraftValidationResultDto(
            draftId,
            true,
            new List<DraftValidationMessageDto>
            {
                new("SAVINGSPLAN_DUE", "Information", "Sparplan ist faellig.", draftId, null, "savings-plans", relatedId)
            });

        // Act
        RenderFragment fragment = builder =>
        {
            builder.OpenComponent(0, typeof(ValidationResultPanel));
            builder.AddAttribute(1, nameof(ValidationResultPanel.ValidationResult), result);
            builder.CloseComponent();
        };

        var cut = Render(fragment);

        // Assert
        var links = cut.FindAll("a");
        Assert.Single(links);
        var expectedBack = Uri.EscapeDataString($"/card/statement-drafts/{draftId}");
        Assert.Equal($"/card/savings-plans/{relatedId}?back={expectedBack}", links[0].GetAttribute("href"));
    }

    /// <summary>
    /// Ensures that entry-specific messages are not treated as general hints in the panel.
    /// </summary>
    [Fact]
    public void EntrySpecificMessage_WithRelatedRecord_DoesNotRenderCardLink()
    {
        // Arrange
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
        Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

        var draftId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var relatedId = Guid.NewGuid();
        var result = new DraftValidationResultDto(
            draftId,
            false,
            new List<DraftValidationMessageDto>
            {
                new("ENTRY_HINT", "Information", "Entry-specific hint.", draftId, entryId, "savings-plans", relatedId)
            });

        // Act
        RenderFragment fragment = builder =>
        {
            builder.OpenComponent(0, typeof(ValidationResultPanel));
            builder.AddAttribute(1, nameof(ValidationResultPanel.ValidationResult), result);
            builder.CloseComponent();
        };

        var cut = Render(fragment);

        // Assert
        Assert.Empty(cut.FindAll("a"));
    }
}
