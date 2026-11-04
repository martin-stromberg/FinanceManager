using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.ViewModels.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Tests of <see cref="McpAuditViewModel"/> (F-0602): timeline loading with filters, drilling
/// (expand/collapse), the integrity check and the export URL building — all exclusively via
/// <see cref="IApiClient"/>.
/// </summary>
public sealed class McpAuditViewModelTests
{
    private sealed class DummyGenericLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private static (McpAuditViewModel vm, Mock<IApiClient> api) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(DummyGenericLocalizer<>));
        var api = new Mock<IApiClient>();
        services.AddSingleton(api.Object);
        var vm = new McpAuditViewModel(services.BuildServiceProvider(), api.Object);
        return (vm, api);
    }

    private static McpAuditEntryListItemDto Item(Guid id, Guid? accessId = null)
        => new(id, DateTime.UtcNow, 1, 0, "ToolCall", "Success",
            accessId, "acc", "fm_postings_query", null, "McpEndpoint", 10, 3, 0);

    /// <summary>Load populates accesses and the first timeline page.</summary>
    [Fact]
    public async Task Load_PopulatesAccessesAndEntries()
    {
        var (vm, api) = CreateVm();
        api.Setup(a => a.Mcp_ListAccessesAsync(null, 0, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new McpAccessListItemDto(Guid.NewGuid(), "acc", null, 0, 0, 0, null, false, null, DateTime.UtcNow, null, DateTime.UtcNow) });
        api.Setup(a => a.Mcp_ListAuditEntriesAsync(null, null, null, null, null, null, null, 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Item(Guid.NewGuid()) });

        await vm.LoadAsync(Guid.Empty);

        Assert.Single(vm.Accesses);
        Assert.Single(vm.Entries);
        Assert.False(vm.CanLoadMore);
        Assert.False(vm.IsMcpDisabled);
    }

    /// <summary>Filters are forwarded to the API when applying.</summary>
    [Fact]
    public async Task ApplyFilters_ForwardsFilterState()
    {
        var (vm, api) = CreateVm();
        var accessId = Guid.NewGuid();
        api.Setup(a => a.Mcp_ListAccessesAsync(null, 0, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<McpAccessListItemDto>());
        api.Setup(a => a.Mcp_ListAuditEntriesAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), (short?)0, accessId, null,
                "fm_x", "Denied", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Item(Guid.NewGuid(), accessId) });

        vm.SetFilterAccess(accessId);
        vm.SetFilterCategory(0);
        vm.SetFilterTool("fm_x");
        vm.SetFilterResult("Denied");
        await vm.ApplyFiltersAsync();

        Assert.Single(vm.Entries);
        Assert.True(vm.OnlyDenied);
        api.Verify(a => a.Mcp_ListAuditEntriesAsync(
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), (short?)0, accessId, null,
            "fm_x", "Denied", 0, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Toggle details loads the entry detail once and collapses on the second call.</summary>
    [Fact]
    public async Task ToggleDetails_LoadsDetail_AndCollapses()
    {
        var (vm, api) = CreateVm();
        var id = Guid.NewGuid();
        var detail = new McpAuditEntryDetailDto(id, DateTime.UtcNow, 1, 0, "ToolCall", "Success",
            Guid.NewGuid(), "acc", "fm_postings_query", "katalog-v1", "McpEndpoint",
            null, null, 10, 3, "{}", null, null, "hash",
            new[] { new McpAuditChangeDto("Posting", Guid.NewGuid(), "ContactId", null, "x") },
            DateTime.UtcNow);
        api.Setup(a => a.Mcp_GetAuditEntryAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(detail);

        await vm.ToggleDetailsAsync(id);
        Assert.Equal(id, vm.ExpandedId);
        Assert.Same(detail, vm.Detail);
        Assert.Single(vm.Detail.Changes);

        await vm.ToggleDetailsAsync(id);
        Assert.Null(vm.ExpandedId);
        Assert.Null(vm.Detail);
        api.Verify(a => a.Mcp_GetAuditEntryAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verify stores the chain verification result for display.</summary>
    [Fact]
    public async Task Verify_StoresResult()
    {
        var (vm, api) = CreateVm();
        var expected = new McpAuditVerifyResultDto(3, true, null, null, Array.Empty<McpAuditChainGapDto>());
        api.Setup(a => a.Mcp_VerifyAuditChainAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        await vm.VerifyAsync();

        Assert.Same(expected, vm.Verification);
    }

    /// <summary>The export URL carries the current filter state.</summary>
    [Fact]
    public void ExportUrl_CarriesFilters()
    {
        var (vm, api) = CreateVm();
        var accessId = Guid.NewGuid();
        api.Setup(a => a.Mcp_BuildAuditExportUrl("csv", null, null, null, accessId, null, null, "Denied"))
            .Returns($"/api/mcp-audit/export?format=csv&mcpAccessId={accessId}&result=Denied");
        vm.SetFilterAccess(accessId);
        vm.SetFilterResult("Denied");

        var url = vm.BuildExportUrl("csv");

        Assert.Contains("format=csv", url);
        Assert.Contains($"mcpAccessId={accessId}", url);
        Assert.Contains("result=Denied", url);
    }

    /// <summary>Load surfaces Err_Mcp_Disabled as a friendly flag instead of an error only.</summary>
    [Fact]
    public async Task Load_SetsDisabledFlag_OnErrMcpDisabled()
    {
        var (vm, api) = CreateVm();
        api.SetupGet(a => a.LastErrorCode).Returns("Err_Mcp_Disabled");
        api.SetupGet(a => a.LastError).Returns("disabled");
        api.Setup(a => a.Mcp_ListAccessesAsync(null, 0, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<McpAccessListItemDto>());
        api.Setup(a => a.Mcp_ListAuditEntriesAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<short?>(), It.IsAny<Guid?>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), 0, 50, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await vm.LoadAsync(Guid.Empty);

        Assert.True(vm.IsMcpDisabled);
        Assert.Empty(vm.Entries);
    }
}
