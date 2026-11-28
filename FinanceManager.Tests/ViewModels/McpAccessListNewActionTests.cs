using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers the "New" action of <see cref="McpAccessListViewModel"/> when the MCP integration is
/// disabled server-side (#605): the ribbon action must be disabled, the shared wizard entry point
/// must reject the request, and the list keeps showing its disabled hint.
/// </summary>
public sealed class McpAccessListNewActionTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private sealed class TestLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }

    private static IServiceProvider CreateSp(Mock<IApiClient> apiMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(apiMock.Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(TestLocalizer<>));
        return services.BuildServiceProvider();
    }

    private static Mock<IApiClient> CreateApiMock(bool mcpEnabled)
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Mcp_GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpStatusDto(mcpEnabled));
        apiMock.Setup(a => a.Mcp_ListAccessesAsync(
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<McpAccessListItemDto>());
        return apiMock;
    }

    private static UiRibbonAction FindNewAction(McpAccessListViewModel vm, IServiceProvider sp)
    {
        var loc = sp.GetRequiredService<IStringLocalizer<McpAccessListNewActionTests>>();
        return (vm.GetRibbonRegisters(loc) ?? Array.Empty<UiRibbonRegister>())
            .SelectMany(r => r.Tabs ?? new List<UiRibbonTab>())
            .SelectMany(t => t.Items)
            .Single(i => i.Id == "New");
    }

    /// <summary>
    /// With a disabled MCP integration the New action must be disabled, the shared wizard
    /// entry point must not raise the overlay action, and the disabled list hint stays.
    /// </summary>
    [Fact]
    public async Task New_McpDisabled_IsDisabledAndWizardIsRejected()
    {
        var apiMock = CreateApiMock(mcpEnabled: false);
        var sp = CreateSp(apiMock);
        var vm = new McpAccessListViewModel(sp);

        await vm.LoadAsync();

        Assert.True(vm.IsMcpDisabled);
        Assert.True(FindNewAction(vm, sp).Disabled);

        string? raised = null;
        vm.UiActionRequested += (_, e) => raised = e?.Action;
        vm.RequestCreateWizard();

        Assert.Null(raised);
        // the list hint record is preserved
        Assert.Equal(1, vm.Records?.Count);
        apiMock.Verify(a => a.Mcp_ListAccessesAsync(
            It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// With an enabled MCP integration the New action stays enabled and the wizard request
    /// raises the overlay action as before.
    /// </summary>
    [Fact]
    public async Task New_McpEnabled_EnabledAndWizardIsRaised()
    {
        var apiMock = CreateApiMock(mcpEnabled: true);
        var sp = CreateSp(apiMock);
        var vm = new McpAccessListViewModel(sp);

        await vm.LoadAsync();

        Assert.False(vm.IsMcpDisabled);
        Assert.False(FindNewAction(vm, sp).Disabled);

        string? raised = null;
        object? payload = null;
        vm.UiActionRequested += (_, e) => { raised = e?.Action; payload = e?.PayloadObject; };
        vm.RequestCreateWizard();

        Assert.Equal("New", raised);
        Assert.IsType<BaseViewModel.UiOverlaySpec>(payload);
    }

    /// <summary>
    /// A failing status call is best-effort: the list keeps loading and the New action stays
    /// enabled — the create wizard re-checks the status itself.
    /// </summary>
    [Fact]
    public async Task StatusCallFails_ListStillLoadsAndNewStaysEnabled()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Mcp_GetStatusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("network"));
        apiMock.Setup(a => a.Mcp_ListAccessesAsync(
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<McpAccessListItemDto>());
        var sp = CreateSp(apiMock);
        var vm = new McpAccessListViewModel(sp);

        await vm.LoadAsync();

        Assert.False(vm.IsMcpDisabled);
        Assert.False(FindNewAction(vm, sp).Disabled);
    }

    /// <summary>
    /// When the list call itself reports <c>Err_Mcp_Disabled</c> the fallback marks the
    /// integration as disabled: hint record is rendered and the New action is disabled.
    /// </summary>
    [Fact]
    public async Task ListCallFailsWithErrMcpDisabled_MarksDisabledAndShowsHint()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Mcp_GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpStatusDto(true));
        apiMock.Setup(a => a.Mcp_ListAccessesAsync(
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("disabled"));
        apiMock.SetupGet(a => a.LastErrorCode).Returns("Err_Mcp_Disabled");
        var sp = CreateSp(apiMock);
        var vm = new McpAccessListViewModel(sp);

        await vm.LoadAsync();

        Assert.True(vm.IsMcpDisabled);
        Assert.True(FindNewAction(vm, sp).Disabled);
        var record = Assert.Single(vm.Records ?? new List<ListRecord>());
        Assert.Contains(record.Cells, c => c.Text == "Mcp_Disabled_Hint");
    }
}
