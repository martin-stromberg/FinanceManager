using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

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

    [Fact]
    public async Task Save_New_Fail()
    {
        var (vm, apiMock) = CreateVm();
        apiMock.Setup(a => a.SecurityCategories_CreateAsync(It.IsAny<SecurityCategoryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SecurityCategoryDto?)null);
        apiMock.SetupGet(a => a.LastError).Returns("bad");

        await vm.InitializeAsync(Guid.Empty);
        vm.Model.Name = "X";
        var ok = await vm.SaveAsync();

        Assert.False(ok);
        Assert.Equal("bad", vm.LastError);
    }

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

    [Fact]
    public async Task Ribbon_Disables_Save_When_Name_Short()
    {
        var (vm, _) = CreateVm();
        var loc = new TestLocalizer<SecurityCategoryDetailViewModelTests>();

        // initialize to ensure CardRecord is available
        await vm.InitializeAsync(Guid.Empty);

        var registers = vm.GetRibbon(loc);
        Assert.True(registers.Count == 1);

        var groups = registers.SelectMany(r => r.Tabs).ToList();
        Assert.True(groups.Count == 2);

        var manage = groups.First(g => g.Title == "Ribbon_Group_Manage");
        var manageActions = manage.Items;
        var save = manageActions.First(i => i.Action == "Save");
        Assert.True(save.Disabled);

        // simulate editing the name via pending field to enable Save
        var nameField = vm.CardRecord?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_SecurityCategory_Name");
        Assert.NotNull(nameField);
        vm.ValidateFieldValue(nameField!, "OK");

        registers = vm.GetRibbon(loc);
        groups = registers.SelectMany(r => r.Tabs).ToList();
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
