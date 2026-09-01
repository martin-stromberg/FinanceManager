using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <see cref="SetupAttachmentCategoriesViewModel"/>'s full inline CRUD workflow for attachment
/// categories in the setup screen: alphabetically sorted loading, add (with input reset and success flag),
/// edit (begin/save), and delete, each verified to leave the in-memory items collection consistent with
/// what was just persisted via the API.
/// </summary>
public sealed class SetupAttachmentCategoriesViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static (SetupAttachmentCategoriesViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        var sp = services.BuildServiceProvider();
        var vm = new SetupAttachmentCategoriesViewModel(sp);
        return (vm, apiMock);
    }

    /// <summary>
    /// Verifies that loaded categories are presented in alphabetical order by name even though the API
    /// returns them out of order.
    /// </summary>
    [Fact]
    public async Task Initialize_Loads_And_Sorts()
    {
        var (vm, apiMock) = CreateVm();
        var categories = new List<AttachmentCategoryDto>
        {
            new AttachmentCategoryDto(Guid.NewGuid(), "B", false, false),
            new AttachmentCategoryDto(Guid.NewGuid(), "A", false, false)
        };
        apiMock.Setup(a => a.Attachments_ListCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal(new[] { "A", "B" }, vm.Items.Select(x => x.Name).ToArray());
    }

    /// <summary>
    /// Verifies that adding a new category via <c>NewName</c> creates it through the API, appends it to
    /// the items list, resets the input field back to empty, and flags the action as successful - the
    /// standard "created and cleared the form" contract the inline add UI depends on.
    /// </summary>
    [Fact]
    public async Task AddAsync_Adds_And_Clears_And_Sets_ActionOk()
    {
        var (vm, apiMock) = CreateVm();
        var createdId = Guid.NewGuid();
        var created = new AttachmentCategoryDto(createdId, "Zeta", false, false);

        apiMock.Setup(a => a.Attachments_ListCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttachmentCategoryDto>());
        apiMock.Setup(a => a.Attachments_CreateCategoryAsync("Zeta", It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.NewName = "Zeta";
        await vm.AddAsync(TestContext.Current.CancellationToken);

        Assert.True(vm.ActionOk);
        Assert.Equal(string.Empty, vm.NewName);
        Assert.Single(vm.Items);
        Assert.Equal("Zeta", vm.Items[0].Name);
    }

    /// <summary>
    /// Verifies the inline-edit round-trip: beginning an edit populates <c>EditId</c>, and saving the
    /// edited name updates the item in place, resets <c>EditId</c> back to empty, and flags the action as
    /// successful - the contract the inline "click to rename" UI relies on.
    /// </summary>
    [Fact]
    public async Task BeginEdit_And_SaveEdit_Updates_Item()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var initial = new AttachmentCategoryDto(id, "Old", false, false);
        var updated = new AttachmentCategoryDto(id, "New", false, false);

        apiMock.Setup(a => a.Attachments_ListCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttachmentCategoryDto> { initial });
        apiMock.Setup(a => a.Attachments_UpdateCategoryNameAsync(id, "New", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        vm.BeginEdit(id, "Old");
        Assert.Equal(id, vm.EditId);
        vm.EditName = "New";
        await vm.SaveEditAsync(TestContext.Current.CancellationToken);

        Assert.True(vm.ActionOk);
        Assert.Equal(Guid.Empty, vm.EditId);
        Assert.Single(vm.Items);
        Assert.Equal("New", vm.Items[0].Name);
    }

    /// <summary>
    /// Verifies that deleting a category removes it from the items collection and flags the action as
    /// successful once the API confirms the deletion.
    /// </summary>
    [Fact]
    public async Task Delete_Removes_Item()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var initial = new AttachmentCategoryDto(id, "ToDelete", false, false);

        apiMock.Setup(a => a.Attachments_ListCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttachmentCategoryDto> { initial });
        apiMock.Setup(a => a.Attachments_DeleteCategoryAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Single(vm.Items);
        await vm.DeleteAsync(id, TestContext.Current.CancellationToken);
        Assert.True(vm.ActionOk);
        Assert.Empty(vm.Items);
    }
}
