using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using FinanceManager.Web.ViewModels.SavingsPlans.Categories;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <see cref="SavingsPlanCategoryCardViewModel"/>'s load behavior for an existing category,
/// including the not-found error path, and that ribbon registers can be built.
/// </summary>
public sealed class SavingsPlanCategoryDetailViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static (SavingsPlanCategoryCardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        var sp = services.BuildServiceProvider();
        var vm = new SavingsPlanCategoryCardViewModel(sp);
        return (vm, apiMock);
    }

    /// <summary>
    /// Verifies that loading an existing category by id populates the card's name field from the API response.
    /// </summary>
    [Fact]
    public async Task Initialize_Edit_Loads_Model()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = new SavingsPlanCategoryDto { Id = id, Name = "Cat1" };
        apiMock.Setup(a => a.SavingsPlanCategories_GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        await vm.LoadAsync(id);

        Assert.Equal("Cat1", vm.Name);
    }

    /// <summary>
    /// Verifies that loading a category id the API cannot find (returns <see langword="null"/>) sets a
    /// non-empty <c>LastError</c> instead of leaving the card silently blank, so the user gets feedback that
    /// the requested category no longer exists.
    /// </summary>
    [Fact]
    public async Task Initialize_Edit_NotFound_Sets_Error()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        apiMock.Setup(a => a.SavingsPlanCategories_GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SavingsPlanCategoryDto?)null);

        await vm.LoadAsync(id);

        Assert.False(string.IsNullOrWhiteSpace(vm.LastError));
    }

    /// <summary>
    /// Smoke test verifying that <c>GetRibbonRegisters</c> returns a non-null result, guarding against
    /// a null-reference regression in ribbon construction rather than asserting specific actions.
    /// </summary>
    [Fact]
    public void Ribbon_Has_Actions()
    {
        var (vm, _) = CreateVm();
        var loc = new TestLocalizer<SavingsPlanCategoryDetailViewModelTests>();
        var regs = vm.GetRibbonRegisters(loc);
        Assert.NotNull(regs);
    }

    /// <summary>
    /// The "Zurücksetzen" action must sit in the manage group directly behind Save as a small,
    /// disabled-by-default item with the undo icon and no mobile shortcut.
    /// </summary>
    [Fact]
    public async Task Ribbon_ContainsReset_AfterSave_InManageGroup()
    {
        var (vm, apiMock) = CreateVm();
        var loc = new TestLocalizer<SavingsPlanCategoryDetailViewModelTests>();
        var id = Guid.NewGuid();
        apiMock.Setup(a => a.SavingsPlanCategories_GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanCategoryDto { Id = id, Name = "Cat1" });
        await vm.LoadAsync(id);

        var manage = FindTab(vm, loc, "Ribbon_Group_Manage");
        var ids = manage.Items.Select(i => i.Id).ToList();
        var saveIndex = ids.IndexOf("Save");
        Assert.True(saveIndex >= 0);
        Assert.Equal(saveIndex + 1, ids.IndexOf("Reset"));

        var reset = manage.Items[saveIndex + 1];
        Assert.Equal(FinanceManager.Web.ViewModels.Common.UiRibbonItemSize.Small, reset.Size);
        Assert.Contains("#undo", reset.IconSvg);
        Assert.Equal("Ribbon_Reset", reset.Label);
        Assert.False(reset.MobileShortcut);
        Assert.True(reset.Disabled);
    }

    /// <summary>
    /// Reset discards the pending name edit and rebuilds the card record from the last loaded
    /// name without re-fetching the category.
    /// </summary>
    [Fact]
    public async Task Reset_DiscardsPendingChanges()
    {
        var (vm, apiMock) = CreateVm();
        var loc = new TestLocalizer<SavingsPlanCategoryDetailViewModelTests>();
        var id = Guid.NewGuid();
        apiMock.Setup(a => a.SavingsPlanCategories_GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanCategoryDto { Id = id, Name = "Cat1" });
        await vm.LoadAsync(id);

        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_SavingsPlanCategory_Name");
        vm.ValidateFieldValue(nameField, "NewName");
        Assert.True(vm.HasPendingChanges);

        var reset = FindAction(vm, loc, "Reset");
        Assert.False(reset.Disabled);
        await reset.Callback!();

        Assert.False(vm.HasPendingChanges);
        Assert.Equal("Cat1", vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_SavingsPlanCategory_Name").Text);
        apiMock.Verify(a => a.SavingsPlanCategories_GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// When the update call fails the save path must roll the model baseline back to the
    /// persisted state, so a subsequent Reset rebuilds the card from the persisted name -
    /// not from the unsaved value that had been copied into the model before the API call.
    /// </summary>
    [Fact]
    public async Task Reset_AfterFailedSave_RestoresPersistedBaseline()
    {
        var (vm, apiMock) = CreateVm();
        var loc = new TestLocalizer<SavingsPlanCategoryDetailViewModelTests>();
        var id = Guid.NewGuid();
        apiMock.Setup(a => a.SavingsPlanCategories_GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanCategoryDto { Id = id, Name = "Cat1" });
        apiMock.Setup(a => a.SavingsPlanCategories_UpdateAsync(id, It.IsAny<SavingsPlanCategoryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SavingsPlanCategoryDto?)null);
        await vm.LoadAsync(id);

        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_SavingsPlanCategory_Name");
        vm.ValidateFieldValue(nameField, "NewName");
        vm.ApplyPendingValues(vm.CardRecord!);

        var ok = await vm.SaveAsync();
        Assert.False(ok);
        Assert.Equal("Cat1", vm.Name);

        await FindAction(vm, loc, "Reset").Callback!();

        Assert.False(vm.HasPendingChanges);
        Assert.Equal("Cat1", vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_SavingsPlanCategory_Name").Text);
    }

    /// <summary>
    /// When the update succeeds but the subsequent reload fails, the save must still report
    /// success and keep the just-persisted values as the reset baseline - it must not roll
    /// the model back to the pre-save state.
    /// </summary>
    [Fact]
    public async Task Save_Success_ReloadFailure_KeepsPersistedBaseline()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        apiMock.SetupSequence(a => a.SavingsPlanCategories_GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanCategoryDto { Id = id, Name = "Cat1" })
            .ReturnsAsync((SavingsPlanCategoryDto?)null);
        apiMock.Setup(a => a.SavingsPlanCategories_UpdateAsync(id, It.IsAny<SavingsPlanCategoryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanCategoryDto { Id = id, Name = "Updated" });
        await vm.LoadAsync(id);

        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_SavingsPlanCategory_Name");
        vm.ValidateFieldValue(nameField, "Updated");
        vm.ApplyPendingValues(vm.CardRecord!);

        var ok = await vm.SaveAsync();

        Assert.True(ok);
        Assert.Equal("Updated", vm.Name);
        Assert.False(vm.HasPendingChanges);
    }

    /// <summary>
    /// In new-create mode the card starts empty (no InitPrefill support on this card); Reset
    /// must restore that empty state.
    /// </summary>
    [Fact]
    public async Task Reset_NewMode_RestoresEmptyState()
    {
        var (vm, apiMock) = CreateVm();
        var loc = new TestLocalizer<SavingsPlanCategoryDetailViewModelTests>();
        await vm.LoadAsync(Guid.Empty);

        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_SavingsPlanCategory_Name");
        vm.ValidateFieldValue(nameField, "Typed name");

        await FindAction(vm, loc, "Reset").Callback!();

        Assert.False(vm.HasPendingChanges);
        Assert.Equal(string.Empty, vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_SavingsPlanCategory_Name").Text);
        apiMock.Verify(a => a.SavingsPlanCategories_CreateAsync(It.IsAny<SavingsPlanCategoryDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static FinanceManager.Web.ViewModels.Common.UiRibbonTab FindTab(SavingsPlanCategoryCardViewModel vm, IStringLocalizer loc, string title)
        => vm.GetRibbonRegisters(loc)!
            .SelectMany(r => r.Tabs ?? Enumerable.Empty<FinanceManager.Web.ViewModels.Common.UiRibbonTab>())
            .Single(t => t.Title == title);

    private static FinanceManager.Web.ViewModels.Common.UiRibbonAction FindAction(SavingsPlanCategoryCardViewModel vm, IStringLocalizer loc, string actionId)
        => vm.GetRibbonRegisters(loc)!
            .SelectMany(r => r.Tabs ?? Enumerable.Empty<FinanceManager.Web.ViewModels.Common.UiRibbonTab>())
            .SelectMany(t => t.Items)
            .Single(a => a.Id == actionId);

    private sealed class TestLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }
}
