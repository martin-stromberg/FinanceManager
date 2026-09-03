using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <c>SecurityCategoryCardViewModel</c>'s full CRUD lifecycle (load, create, update, delete,
/// including their respective failure paths surfacing <c>LastError</c>) and the ribbon's Save action being
/// disabled until the name field is actually edited.
/// </summary>
public sealed class SecurityCategoryDetailViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static (FinanceManager.Web.ViewModels.Securities.Categories.SecurityCategoryCardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        var sp = services.BuildServiceProvider();
        var vm = new FinanceManager.Web.ViewModels.Securities.Categories.SecurityCategoryCardViewModel(sp);
        return (vm, apiMock);
    }

    /// <summary>
    /// Verifies that loading an existing category by id populates the model's name and leaves no error set.
    /// </summary>
    [Fact]
    public async Task Initialize_Edit_Loads_Model()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = new SecurityCategoryDto { Id = id, Name = "Cat1" };
        apiMock.Setup(a => a.SecurityCategories_GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        await vm.InitializeAsync(id);

        Assert.Equal(id, vm.Id);
        Assert.Equal("Cat1", vm.Model.Name);
        Assert.Null(vm.LastError);
    }

    /// <summary>
    /// Verifies that loading a category id the API cannot find (returns <see langword="null"/>) sets a
    /// "Not found" error, so the user gets feedback instead of a silently empty card.
    /// </summary>
    [Fact]
    public async Task Initialize_Edit_NotFound_Sets_Error()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        apiMock.Setup(a => a.SecurityCategories_GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SecurityCategoryDto?)null);

        await vm.InitializeAsync(id);

        Assert.Equal(id, vm.Id);
        Assert.Equal("Not found", vm.LastError);
    }

    /// <summary>
    /// Verifies that saving a new category (via a pending field edit applied to the card record) calls
    /// the create API with the entered name and reports success with no error.
    /// </summary>
    [Fact]
    public async Task Save_New_Success()
    {
        var (vm, apiMock) = CreateVm();
        var createdDto = new SecurityCategoryDto { Id = Guid.NewGuid(), Name = "NewCat" };
        apiMock.Setup(a => a.SecurityCategories_CreateAsync(It.IsAny<SecurityCategoryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdDto);
        // ensure subsequent GET for the created id returns the created dto so LoadAsync does not set an error
        apiMock.Setup(a => a.SecurityCategories_GetAsync(createdDto.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdDto);

        await vm.InitializeAsync(Guid.Empty);
        // set the card field text (and pending) so SaveAsync picks up the new name
        var nameField = vm.CardRecord?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_SecurityCategory_Name");
        Assert.NotNull(nameField);
        vm.ValidateFieldValue(nameField!, "NewCat");
        // Apply pending values to the CardRecord so SaveAsync reads the updated field.Text
        vm.ApplyPendingValues(vm.CardRecord!);

        var ok = await vm.SaveAsync();

        Assert.True(ok);
        Assert.Null(vm.LastError);
        apiMock.Verify(a => a.SecurityCategories_CreateAsync(It.Is<SecurityCategoryRequest>(r => r.Name == "NewCat"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that a failed create call (API returns <see langword="null"/>) reports failure and copies
    /// the API's <c>LastError</c> onto the view model so the failure reason reaches the user.
    /// </summary>
    [Fact]
    public async Task Save_New_Fail()
    {
        var (vm, apiMock) = CreateVm();
        apiMock.Setup(a => a.SecurityCategories_CreateAsync(It.IsAny<SecurityCategoryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SecurityCategoryDto)null!);
        apiMock.SetupGet(a => a.LastError).Returns("bad");

        await vm.InitializeAsync(Guid.Empty);
        vm.Model.Name = "X";
        var ok = await vm.SaveAsync();

        Assert.False(ok);
        Assert.Equal("bad", vm.LastError);
    }

    /// <summary>
    /// Verifies that saving an edited existing category succeeds and clears any prior error.
    /// </summary>
    [Fact]
    public async Task Save_Edit_Success()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var existingDto = new SecurityCategoryDto { Id = id, Name = "Cat" };
        var updatedDto = new SecurityCategoryDto { Id = id, Name = "Updated" };

        apiMock.Setup(a => a.SecurityCategories_GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);
        apiMock.Setup(a => a.SecurityCategories_UpdateAsync(id, It.IsAny<SecurityCategoryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedDto);

        await vm.InitializeAsync(id);
        vm.Model.Name = "Updated";
        var ok = await vm.SaveAsync();

        Assert.True(ok);
        Assert.Null(vm.LastError);
    }

    /// <summary>
    /// Verifies that deleting a loaded category returns success when the API confirms the deletion.
    /// </summary>
    [Fact]
    public async Task Delete_Success()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var existingDto = new SecurityCategoryDto { Id = id, Name = "Cat" };

        apiMock.Setup(a => a.SecurityCategories_GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);
        apiMock.Setup(a => a.SecurityCategories_DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await vm.InitializeAsync(id);
        var ok = await vm.DeleteAsync();

        Assert.True(ok);
    }

    /// <summary>
    /// Verifies that a failed deletion (API returns <see langword="false"/>) reports failure and copies
    /// the API's <c>LastError</c> onto the view model.
    /// </summary>
    [Fact]
    public async Task Delete_Fail()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var existingDto = new SecurityCategoryDto { Id = id, Name = "Cat" };

        apiMock.Setup(a => a.SecurityCategories_GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);
        apiMock.Setup(a => a.SecurityCategories_DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        apiMock.SetupGet(a => a.LastError).Returns("oops");

        await vm.InitializeAsync(id);
        var ok = await vm.DeleteAsync();

        Assert.False(ok);
        Assert.Equal("oops", vm.LastError);
    }

    /// <summary>
    /// Verifies the ribbon's dirty-state gating for Save: a freshly initialized new-category card starts
    /// with Save disabled (no pending edits yet), and entering a name into the pending field flips Save to
    /// enabled - guarding against the user saving an unedited, effectively empty category.
    /// </summary>
    [Fact]
    public async Task Ribbon_Disables_Save_When_Name_Short()
    {
        var (vm, _) = CreateVm();
        var loc = new TestLocalizer<SecurityCategoryDetailViewModelTests>();

        // initialize to ensure CardRecord is available
        await vm.InitializeAsync(Guid.Empty);

        var registers = vm.GetRibbon(loc)!;
        Assert.True(registers.Count == 1);

        var groups = registers.SelectMany(r => r.Tabs ?? new List<FinanceManager.Web.ViewModels.Common.UiRibbonTab>()).ToList();
        Assert.True(groups.Count == 2);

        var manage = groups.First(g => g.Title == "Ribbon_Group_Manage");
        var manageActions = manage.Items;
        var save = manageActions.First(i => i.Action == "Save");
        Assert.True(save.Disabled);

        // simulate editing the name via pending field to enable Save
        var nameField = vm.CardRecord?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_SecurityCategory_Name");
        Assert.NotNull(nameField);
        vm.ValidateFieldValue(nameField!, "OK");

        registers = vm.GetRibbon(loc)!;
        groups = registers.SelectMany(r => r.Tabs ?? new List<FinanceManager.Web.ViewModels.Common.UiRibbonTab>()).ToList();
        manage = groups.First(g => g.Title == "Ribbon_Group_Manage");
        manageActions = manage.Items;
        save = manageActions.First(i => i.Action == "Save");
        Assert.False(save.Disabled);
    }

    private sealed class TestLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }
}
