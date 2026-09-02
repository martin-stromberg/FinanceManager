using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end test for the admin IP-block management API, covering create, explicit block/unblock,
/// counter reset and delete for a blocked address.
/// </summary>
public class ApiClientIpBlocksTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiClientIpBlocksTests"/> class.
    /// </summary>
    /// <param name="factory">Shared web application factory providing the in-memory test server.</param>
    public ApiClientIpBlocksTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return new FinanceManager.Shared.ApiClient(http);
    }

    /// <summary>
    /// Verifies the admin IP-block workflow end to end as an authenticated admin: list, create an
    /// unblocked entry, update it to blocked, explicitly block/unblock, reset abuse counters, and
    /// delete - guards the security-relevant admin surface that controls IP-based access restriction.
    /// </summary>
    [Fact]
    public async Task IpBlocks_List_Create_Block_Unblock_Delete()
    {
        var api = CreateClient();
        await api.Auth_LoginAsync(new LoginRequest(TestWebApplicationFactory.BootstrapAdminUsername, TestWebApplicationFactory.BootstrapAdminPassword, null, null), TestContext.Current.CancellationToken);

        var list = await api.Admin_ListIpBlocksAsync(ct: TestContext.Current.CancellationToken);
        list.Should().NotBeNull();

        // Create
        var created = await api.Admin_CreateIpBlockAsync(new IpBlockCreateRequest("1.2.3.4", "test", IsBlocked: false), TestContext.Current.CancellationToken);
        created.IpAddress.Should().Be("1.2.3.4");
        created.IsBlocked.Should().BeFalse();

        // Update (block)
        var updated = await api.Admin_UpdateIpBlockAsync(created.Id, new IpBlockUpdateRequest("changed", IsBlocked: true), TestContext.Current.CancellationToken);
        updated!.IsBlocked.Should().BeTrue();

        // Block explicitly
        var okBlock = await api.Admin_BlockIpAsync(created.Id, "now block", CancellationToken.None);
        okBlock.Should().BeTrue();

        // Unblock
        var okUnblock = await api.Admin_UnblockIpAsync(created.Id, TestContext.Current.CancellationToken);
        okUnblock.Should().BeTrue();

        // Reset counters
        var okReset = await api.Admin_ResetCountersAsync(created.Id, TestContext.Current.CancellationToken);
        okReset.Should().BeTrue();

        // Delete
        var okDel = await api.Admin_DeleteIpBlockAsync(created.Id, TestContext.Current.CancellationToken);
        okDel.Should().BeTrue();
    }
}
