using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <c>SetupStatementsViewModel</c>'s import-split settings: loading, the field-combination
/// validation rules governing when a split is well-formed, the save/dirty-reset cycle, and that the
/// mass-import confirmation dialog policy is included in the persisted save request.
/// </summary>
public sealed class SetupImportSplitViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static IServiceProvider CreateSp(IApiClient api)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(api);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Verifies that loading populates the model from the API's import-split settings.
    /// </summary>
    [Fact]
    public async Task Initialize_Loads_Settings()
    {
        var dto = new ImportSplitSettingsDto { Mode = ImportSplitMode.Monthly, MaxEntriesPerDraft = 200, MonthlySplitThreshold = 250, MinEntriesPerDraft = 5 };
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.UserSettings_GetImportSplitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var vm = new SetupStatementsViewModel(CreateSp(apiMock.Object));
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        Assert.False(vm.Loading);
        Assert.NotNull(vm.Model);
        Assert.Equal(ImportSplitMode.Monthly, vm.Model!.Mode);
    }

    /// <summary>
    /// Verifies three distinct import-split validation rules in sequence: a max-entries value below the
    /// allowed minimum is rejected; a monthly-mode split with a zero minimum-entries is rejected; and for
    /// the "monthly or fixed" mode, a split threshold below the max-entries value is rejected while an
    /// equal threshold is accepted - covering the field-interdependency rules the UI must enforce before saving.
    /// </summary>
    [Fact]
    public async Task Validate_Disallows_Invalid_Combinations()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.UserSettings_GetImportSplitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ImportSplitSettingsDto());

        var vm = new SetupStatementsViewModel(CreateSp(apiMock.Object));
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        vm.Model!.MaxEntriesPerDraft = 10;
        vm.Validate();
        Assert.True(vm.HasValidationError);

        vm.Model!.MaxEntriesPerDraft = 100;
        vm.Model!.Mode = ImportSplitMode.Monthly;
        vm.Model!.MinEntriesPerDraft = 0;
        vm.Validate();
        Assert.True(vm.HasValidationError);

        vm.Model!.MinEntriesPerDraft = 10;
        vm.Model!.MonthlySplitThreshold = 100; // equal to max => ok
        vm.Model!.Mode = ImportSplitMode.MonthlyOrFixed;
        vm.Validate();
        Assert.False(vm.HasValidationError);
        vm.Model!.MonthlySplitThreshold = 50; // less than max => error
        vm.Validate();
        Assert.True(vm.HasValidationError);
    }

    /// <summary>
    /// Verifies that editing a setting marks the view model dirty, and that a successful save persists
    /// the change via the update API, sets <c>SavedOk</c>, and clears the dirty flag - the standard
    /// save-confirmation cycle the settings screen's Save button depends on.
    /// </summary>
    [Fact]
    public async Task Save_Sets_SavedOk_And_Resets_Dirty()
    {
        var dto = new ImportSplitSettingsDto();
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.UserSettings_GetImportSplitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.UserSettings_UpdateImportSplitAsync(It.IsAny<ImportSplitSettingsUpdateRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var vm = new SetupStatementsViewModel(CreateSp(apiMock.Object));
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        vm.Model!.MaxEntriesPerDraft = 300;
        vm.OnModeChanged();
        Assert.True(vm.Dirty);

        await vm.SaveAsync(TestContext.Current.CancellationToken);
        apiMock.Verify(a => a.UserSettings_UpdateImportSplitAsync(It.IsAny<ImportSplitSettingsUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(vm.SavedOk);
        Assert.False(vm.Dirty);
    }

    /// <summary>
    /// Verifies that changing the mass-import dialog confirmation policy is actually included in the
    /// persisted update request, guarding against the field being dropped or overwritten with a stale
    /// value if it were forgotten when the request DTO was built.
    /// </summary>
    [Fact]
    public async Task Save_ShouldPersistMassImportDialogPolicy()
    {
        var dto = new ImportSplitSettingsDto { MassImportDialogPolicy = MassImportDialogPolicy.OnMissingInformation };
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.UserSettings_GetImportSplitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        ImportSplitSettingsUpdateRequest? captured = null;
        apiMock
            .Setup(a => a.UserSettings_UpdateImportSplitAsync(It.IsAny<ImportSplitSettingsUpdateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ImportSplitSettingsUpdateRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(true);

        var vm = new SetupStatementsViewModel(CreateSp(apiMock.Object));
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.Model!.MassImportDialogPolicy = MassImportDialogPolicy.AlwaysConfirm;

        await vm.SaveAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(MassImportDialogPolicy.AlwaysConfirm, captured!.MassImportDialogPolicy);
    }
}
