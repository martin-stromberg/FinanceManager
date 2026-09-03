using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Security;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <c>SetupSecurityViewModel</c>'s admin IP block list: loading blocked IPs and creating a new
/// block, including that the create form is cleared and the "block on create" flag stays set after success.
/// </summary>
public sealed class SetupIpBlocksViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; } = true;
    }

    private static (SetupSecurityViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        var sp = services.BuildServiceProvider();
        var vm = new SetupSecurityViewModel(sp);
        return (vm, apiMock);
    }

    /// <summary>
    /// Verifies that reloading as an admin user populates the items list with blocked IP addresses from the API.
    /// </summary>
    [Fact]
    public async Task Initialize_Loads_List_When_Admin()
    {
        var (vm, apiMock) = CreateVm();
        IReadOnlyList<IpBlockDto> items = new List<IpBlockDto>
        {
            new IpBlockDto(Guid.NewGuid(), "1.2.3.4", false, null, null, 0, null, DateTime.UtcNow, null)
        };

        apiMock.Setup(a => a.Admin_ListIpBlocksAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        await vm.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Single(vm.Items);
        Assert.Equal("1.2.3.4", vm.Items[0].IpAddress);
    }

    /// <summary>
    /// Verifies that creating an IP block sends the entered IP address to the API and, on success, clears
    /// the IP and reason input fields while leaving the "block on create" toggle in its set state, so the
    /// form is ready for the next entry.
    /// </summary>
    [Fact]
    public async Task Create_Clears_Form_On_Success()
    {
        var (vm, apiMock) = CreateVm();
        var createdDto = new IpBlockDto(Guid.NewGuid(), "1.2.3.4", true, null, "test", 0, null, DateTime.UtcNow, null);

        apiMock.Setup(a => a.Admin_CreateIpBlockAsync(It.IsAny<IpBlockCreateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdDto);
        apiMock.Setup(a => a.Admin_ListIpBlocksAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<IpBlockDto>)new List<IpBlockDto>());

        vm.Ip = "1.2.3.4";
        vm.Reason = "test";

        await vm.CreateAsync(TestContext.Current.CancellationToken);

        apiMock.Verify(a => a.Admin_CreateIpBlockAsync(It.Is<IpBlockCreateRequest>(r => r.IpAddress == "1.2.3.4"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(string.Empty, vm.Ip);
        Assert.Null(vm.Reason);
        Assert.True(vm.BlockOnCreate);
    }
}
