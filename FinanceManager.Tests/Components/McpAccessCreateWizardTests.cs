using Bunit;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Components.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Components;

/// <summary>
/// Covers the defense-in-depth branch of <see cref="McpAccessCreateWizard"/> for a disabled MCP
/// integration (#605): the wizard must show the disabled hint instead of its steps and must not
/// query level/options endpoints.
/// </summary>
public sealed class McpAccessCreateWizardTests : Bunit.BunitContext
{
    private sealed class TestLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }

    /// <summary>Registers a pass-through localizer returning resource keys.</summary>
    public McpAccessCreateWizardTests()
    {
        Services.AddSingleton(typeof(IStringLocalizer<>), typeof(TestLocalizer<>));
    }

    private Mock<IApiClient> RegisterApi(bool mcpEnabled)
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Mcp_GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpStatusDto(mcpEnabled));
        Services.AddSingleton(apiMock.Object);
        return apiMock;
    }

    /// <summary>
    /// With a disabled MCP integration the wizard renders only the disabled hint plus a cancel
    /// button — no wizard steps — and never asks for levels or options.
    /// </summary>
    [Fact]
    public void DisabledMcp_ShowsHintInsteadOfSteps()
    {
        var apiMock = RegisterApi(mcpEnabled: false);

        var cut = Render<McpAccessCreateWizard>();

        Assert.Contains("Mcp_Disabled_Hint", cut.Markup);
        Assert.Empty(cut.FindAll(".mcp-wizard-steps"));
        Assert.Single(cut.FindAll("button.btn"));
        apiMock.Verify(a => a.Mcp_GetStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
        apiMock.Verify(a => a.Mcp_GetLevelsAsync(It.IsAny<CancellationToken>()), Times.Never);
        apiMock.Verify(a => a.Mcp_GetOptionsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The cancel button in the disabled state invokes the cascading OverlayClose action.
    /// </summary>
    [Fact]
    public void DisabledMcp_CancelInvokesOverlayClose()
    {
        var apiMock = RegisterApi(mcpEnabled: false);
        var closed = false;
        var cut = Render<McpAccessCreateWizard>(p => p
            .AddCascadingValue("OverlayClose", (Action)(() => closed = true)));

        cut.Find("button.btn").Click();

        Assert.True(closed);
    }

    /// <summary>
    /// With an enabled MCP integration the wizard shows its step header as before.
    /// </summary>
    [Fact]
    public void EnabledMcp_ShowsWizardSteps()
    {
        var apiMock = RegisterApi(mcpEnabled: true);
        apiMock.Setup(a => a.Mcp_GetLevelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<McpAccessLevelDto>());
        apiMock.Setup(a => a.Mcp_GetOptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((McpAccessOptionsDto?)null);

        var cut = Render<McpAccessCreateWizard>();

        Assert.DoesNotContain("Mcp_Disabled_Hint", cut.Markup);
        Assert.NotEmpty(cut.FindAll(".mcp-wizard-steps"));
    }
}
