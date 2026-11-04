using FinanceManager.Domain.Mcp;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Services;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers the master-data save confirmations of <see cref="McpAccessCardViewModel"/> — in
/// particular the critical confirmation for switching the approval mode to
/// <see cref="McpApprovalMode.PreApproved"/>: the check must run against the RESULTING scope
/// profile, so a combined save that also upgrades the level to Buchhalter can no longer slip
/// pre-approved booking/deletion through unnoticed (acceptance schritt-5, F-0504 deviation).
/// </summary>
public sealed class McpAccessCardViewModelTests
{
    private sealed class DummyGenericLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private static (McpAccessCardViewModel vm, Mock<IApiClient> api, Mock<IConfirmationService> confirm) CreateVm(
        Guid id, McpAccessDetailDto detail)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(DummyGenericLocalizer<>));
        var api = new Mock<IApiClient>();
        api.Setup(x => x.Mcp_GetAccessAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);
        api.Setup(x => x.Mcp_GetLevelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<McpAccessLevelDto>());
        api.Setup(x => x.Mcp_GetOptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((McpAccessOptionsDto?)null);
        api.Setup(x => x.Mcp_UpdateAccessAsync(id, It.IsAny<McpAccessUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var confirm = new Mock<IConfirmationService>();
        services.AddSingleton(confirm.Object);
        services.AddSingleton(api.Object);
        var vm = new McpAccessCardViewModel(services.BuildServiceProvider(), api.Object);
        return (vm, api, confirm);
    }

    private static McpAccessScopeDto Scope(McpDataType dataType, McpRightKind right)
        => new(Guid.Empty, (short)dataType, (short)right, null, null, null, null, null);

    private static McpAccessDetailDto Detail(
        Guid id,
        McpAccessLevel level,
        McpApprovalMode mode,
        IReadOnlyList<McpAccessScopeDto>? scopes = null)
        => new(
            id,
            "test-access",
            null,
            (short)level,
            (short)mode,
            (short)McpAccessStatus.Active,
            null,
            false,
            null,
            null,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            scopes ?? Array.Empty<McpAccessScopeDto>(),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            null);

    private static void SetPending(McpAccessCardViewModel vm, string labelKey, string value)
        => vm.ValidateFieldValue(new CardField(labelKey, CardFieldKind.Text, text: value, editable: true), value);

    /// <summary>
    /// Verifies that a combined save — level upgrade Analyse → Buchhalter plus approval mode
    /// → PreApproved in the same request — triggers the critical pre-approval confirmation even
    /// though the LOADED profile (read-only analysis scopes) contains no Book/Delete tuples:
    /// the check must evaluate the resulting Buchhalter profile.
    /// </summary>
    [Fact]
    public async Task SaveAsync_LevelUpgradeWithPreApproved_RequestsCriticalConfirmation()
    {
        // Arrange: loaded access is Analyse + PerAction with a read-only effective profile.
        var id = Guid.NewGuid();
        var detail = Detail(id, McpAccessLevel.Analyse, McpApprovalMode.PerAction,
            new[] { Scope(McpDataType.Postings, McpRightKind.Read) });
        var (vm, api, confirm) = CreateVm(id, detail);
        await vm.LoadAsync(id);
        confirm.Setup(x => x.ConfirmAsync(It.IsAny<ConfirmationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act: combined pending change — level up AND approval mode to PreApproved.
        SetPending(vm, "Mcp_Card_Level", nameof(McpAccessLevel.Buchhalter));
        SetPending(vm, "Mcp_Card_ApprovalMode", nameof(McpApprovalMode.PreApproved));
        var ok = await vm.SaveAsync(TestContext.Current.CancellationToken);

        // Assert: critical confirmation requested for the resulting Buchhalter profile.
        Assert.True(ok);
        confirm.Verify(x => x.ConfirmAsync(
            It.Is<ConfirmationRequest>(r =>
                r.Severity == ConfirmationSeverity.Critical
                && r.TitleResourceKey == "Confirmation_McpPreApproved_Title"),
            It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(x => x.Mcp_UpdateAccessAsync(id,
            It.Is<McpAccessUpdateRequest>(r =>
                r.Level == (short)McpAccessLevel.Buchhalter
                && r.ApprovalMode == (short)McpApprovalMode.PreApproved),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that switching the approval mode to PreApproved WITHOUT write rights in the
    /// resulting profile (level stays Analyse — read only) persists without any confirmation.
    /// </summary>
    [Fact]
    public async Task SaveAsync_PreApprovedWithoutWriteRights_NoConfirmation()
    {
        var id = Guid.NewGuid();
        var detail = Detail(id, McpAccessLevel.Analyse, McpApprovalMode.PerAction,
            new[] { Scope(McpDataType.Postings, McpRightKind.Read) });
        var (vm, api, confirm) = CreateVm(id, detail);
        await vm.LoadAsync(id);

        SetPending(vm, "Mcp_Card_ApprovalMode", nameof(McpApprovalMode.PreApproved));
        var ok = await vm.SaveAsync(TestContext.Current.CancellationToken);

        Assert.True(ok);
        confirm.Verify(x => x.ConfirmAsync(It.IsAny<ConfirmationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        api.Verify(x => x.Mcp_UpdateAccessAsync(id, It.IsAny<McpAccessUpdateRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that cancelling the critical confirmation aborts the combined save — the
    /// master-data update must not reach the API.
    /// </summary>
    [Fact]
    public async Task SaveAsync_PreApprovedConfirmationCancelled_SkipsUpdate()
    {
        var id = Guid.NewGuid();
        var detail = Detail(id, McpAccessLevel.Analyse, McpApprovalMode.PerAction,
            new[] { Scope(McpDataType.Postings, McpRightKind.Read) });
        var (vm, api, confirm) = CreateVm(id, detail);
        await vm.LoadAsync(id);
        confirm.Setup(x => x.ConfirmAsync(It.IsAny<ConfirmationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        SetPending(vm, "Mcp_Card_Level", nameof(McpAccessLevel.Buchhalter));
        SetPending(vm, "Mcp_Card_ApprovalMode", nameof(McpApprovalMode.PreApproved));
        var ok = await vm.SaveAsync(TestContext.Current.CancellationToken);

        Assert.False(ok);
        api.Verify(x => x.Mcp_UpdateAccessAsync(It.IsAny<Guid>(), It.IsAny<McpAccessUpdateRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
