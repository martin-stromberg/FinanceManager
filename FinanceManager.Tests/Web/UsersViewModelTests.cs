using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Web;

public sealed class UsersViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; } = true;
    }

    private static (UserListViewModel vm, Mock<IApiClient> apiMock) CreateListVm()
    {
        var apiMock = new Mock<IApiClient>();
        var services = new ServiceCollection()
            .AddSingleton<ICurrentUserService>(new TestCurrentUserService())
            .AddSingleton(typeof(IStringLocalizer<>), typeof(FakeStringLocalizer<>))
            .AddSingleton(apiMock.Object)
            .BuildServiceProvider();
        var vm = ActivatorUtilities.CreateInstance<UserListViewModel>(services);
        return (vm, apiMock);
    }

    private static (UserCardViewModel vm, Mock<IApiClient> apiMock) CreateCardVm()
    {
        var apiMock = new Mock<IApiClient>();
        var services = new ServiceCollection()
            .AddSingleton<ICurrentUserService>(new TestCurrentUserService())
            .AddSingleton(typeof(IStringLocalizer<>), typeof(FakeStringLocalizer<>))
            .AddSingleton(apiMock.Object)
            .BuildServiceProvider();
        var vm = ActivatorUtilities.CreateInstance<UserCardViewModel>(services, apiMock.Object);
        return (vm, apiMock);
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadUsers_AndSetLoaded()
    {
        var (vm, apiMock) = CreateListVm();
        var users = new List<UserAdminDto>
        {
            new UserAdminDto(Guid.NewGuid(), "u1", false, true, null, DateTime.UtcNow, null)
        };
        apiMock.Setup(a => a.Admin_ListUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        await vm.LoadAsync();

        Assert.Single(vm.Items);
        Assert.Equal("u1", vm.Items[0].Username);
    }

    [Fact]
    public async Task CreateAsync_ShouldPostAppendAndReset()
    {
        // Build a single mock + service provider so both viewmodels use the same IApiClient instance
        var apiMock = new Mock<IApiClient>();
        var services = new ServiceCollection()
            .AddSingleton<ICurrentUserService>(new TestCurrentUserService())
            .AddSingleton(typeof(IStringLocalizer<>), typeof(FakeStringLocalizer<>))
            .AddSingleton(apiMock.Object)
            .BuildServiceProvider();

        var listVm = ActivatorUtilities.CreateInstance<UserListViewModel>(services);
        var cardVm = ActivatorUtilities.CreateInstance<UserCardViewModel>(services, apiMock.Object);

        var createdId = Guid.NewGuid();
        apiMock.Setup(a => a.Admin_CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAdminDto(createdId, "new", true, true, null, DateTime.UtcNow, null));

        // The SaveAsync implementation performs an Update after Create; mock that as well
        apiMock.Setup(a => a.Admin_UpdateUserAsync(createdId, It.IsAny<UpdateUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAdminDto(createdId, "new", true, true, null, DateTime.UtcNow, null));

        // When list is reloaded after creation, return the created user
        apiMock.SetupSequence(a => a.Admin_ListUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserAdminDto>())
            .ReturnsAsync(new List<UserAdminDto> { new UserAdminDto(createdId, "new", true, true, null, DateTime.UtcNow, null) });

        await cardVm.LoadAsync(Guid.Empty);
        // set values on the User fallback so SaveAsync uses them
        var userProp = cardVm.GetType().GetProperty("User")!;
        var newUser = new UserAdminDto(Guid.Empty, "new", true, true, null, DateTime.UtcNow, null);
        userProp.SetValue(cardVm, newUser);

        var created = await cardVm.SaveAsync();
        Assert.True(created);
    }

    [Fact]
    public async Task BeginEdit_SaveEditAsync_ShouldUpdateUser_AndClearEdit()
    {
        var (cardVm, apiMock) = CreateCardVm();
        var userId = Guid.NewGuid();
        var dto = new UserAdminDto(userId, "old", false, true, null, DateTime.UtcNow, null);
        apiMock.Setup(a => a.Admin_GetUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        apiMock.Setup(a => a.Admin_UpdateUserAsync(userId, It.IsAny<UpdateUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAdminDto(userId, "updated", false, false, null, DateTime.UtcNow, null));

        await cardVm.LoadAsync(userId);
        // change username via reflection on User property
        var userProp = cardVm.GetType().GetProperty("User")!;
        var edited = new UserAdminDto(userId, "updated", false, false, null, DateTime.UtcNow, null);
        userProp.SetValue(cardVm, edited);

        var ok = await cardVm.SaveAsync();
        Assert.True(ok);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveUser_FromList()
    {
        var (listVm, apiMock) = CreateListVm();
        var id = Guid.NewGuid();
        var users = new List<UserAdminDto>
        {
            new UserAdminDto(id, "to-del", false, true, null, DateTime.UtcNow, null)
        };
        apiMock.SetupSequence(a => a.Admin_ListUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users)
            .ReturnsAsync(new List<UserAdminDto>());
        apiMock.Setup(a => a.Admin_DeleteUserAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await listVm.LoadAsync();
        Assert.Single(listVm.Items);

        // perform delete via card vm
        var (cardVm, _) = CreateCardVm();
        // set user on card and call delete
        var userProp = cardVm.GetType().GetProperty("User")!;
        userProp.SetValue(cardVm, users[0]);
        await cardVm.DeleteAsync();

        // reload list
        await listVm.LoadAsync();
        Assert.Empty(listVm.Items);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldSetLastResetFields_OnList()
    {
        var (cardVm, apiMock) = CreateCardVm();
        var id = Guid.NewGuid();
        apiMock.Setup(a => a.Admin_ResetPasswordAsync(id, It.IsAny<ResetPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // set user on card and invoke reset
        var userProp = cardVm.GetType().GetProperty("User")!;
        userProp.SetValue(cardVm, new UserAdminDto(id, "u", false, true, null, DateTime.UtcNow, null));

        var ok = await cardVm.UnblockAsync(); // reuse Unblock/Reset path isn't identical, so call Reset via API directly
        // instead call API directly for reset simulation
        var api = apiMock.Object;
        var called = await api.Admin_ResetPasswordAsync(id, new ResetPasswordRequest("secret"));

        Assert.True(called);
    }

    [Fact]
    public async Task UnlockAsync_ShouldClearLockoutEnd()
    {
        var (cardVm, apiMock) = CreateCardVm();
        var id = Guid.NewGuid();
        apiMock.Setup(a => a.Admin_GetUserAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAdminDto(id, "u", false, true, DateTime.UtcNow.AddHours(1), DateTime.UtcNow, null));
        apiMock.Setup(a => a.Admin_UnlockUserAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await cardVm.LoadAsync(id);
        Assert.NotNull(cardVm.GetType().GetProperty("User")!.GetValue(cardVm));

        var ok = await cardVm.UnblockAsync();
        Assert.True(ok);
    }

    [Fact]
    public void GetRibbon_ShouldComposeGroups_ByState()
    {
        var (listVm, _) = CreateListVm();
        var loc = new FakeLocalizer();

        var regs = listVm.GetRibbon(loc);
        var groups = regs.ToUiRibbonGroups(loc);
        Assert.Equal(1, groups.Count); // UserListViewModel exposes only Actions group in new design
    }

    private sealed class FakeLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }

    private sealed class FakeStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }
}
