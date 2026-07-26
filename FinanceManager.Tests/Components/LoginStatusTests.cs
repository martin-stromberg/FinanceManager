using Bunit;
using FinanceManager.Application;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Components;
using FinanceManager.Web.Services.Updates;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Tests.Components;

public sealed class LoginStatusTests : BunitContext
{
    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private sealed class FakeInstalledReleaseMetadataProvider : IInstalledReleaseMetadataProvider
    {
        private readonly InstalledReleaseMetadataDto _dto;

        public FakeInstalledReleaseMetadataProvider(InstalledReleaseMetadataDto dto)
        {
            _dto = dto;
        }

        public Task<InstalledReleaseMetadataDto> GetAsync(CancellationToken ct = default)
            => Task.FromResult(_dto);
    }

    [Fact]
    public void RendersVersion_WhenAuthenticated_AndVersionAvailable()
    {
        // Arrange
        Services.AddSingleton<ICurrentUserService>(new FakeCurrentUserService());
        Services.AddSingleton<IInstalledReleaseMetadataProvider>(
            new FakeInstalledReleaseMetadataProvider(new InstalledReleaseMetadataDto("1.2.3", null, null, null, null)));

        // Act
        var cut = Render<LoginStatus>();

        // Assert
        Assert.Contains("1.2.3", cut.Find(".login-status").TextContent);
    }

    [Fact]
    public void RendersFallback_WhenVersionIsNull()
    {
        // Arrange
        Services.AddSingleton<ICurrentUserService>(new FakeCurrentUserService());
        Services.AddSingleton<IInstalledReleaseMetadataProvider>(
            new FakeInstalledReleaseMetadataProvider(new InstalledReleaseMetadataDto(null, null, null, null, null)));

        // Act
        var cut = Render<LoginStatus>();

        // Assert
        Assert.Contains("Version unbekannt", cut.Find(".login-status").TextContent);
    }

    [Fact]
    public void DoesNotRenderUserId_WhenAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        Services.AddSingleton<ICurrentUserService>(new FakeCurrentUserService { UserId = userId });
        Services.AddSingleton<IInstalledReleaseMetadataProvider>(
            new FakeInstalledReleaseMetadataProvider(new InstalledReleaseMetadataDto("1.2.3", null, null, null, null)));

        // Act
        var cut = Render<LoginStatus>();

        // Assert
        var loginStatus = cut.Find(".login-status");
        Assert.DoesNotContain(userId.ToString(), loginStatus.TextContent);
        Assert.Null(loginStatus.GetAttribute("title"));
    }

    [Fact]
    public void RendersLoginLink_WhenNotAuthenticated()
    {
        // Arrange
        Services.AddSingleton<ICurrentUserService>(new FakeCurrentUserService { IsAuthenticated = false });
        Services.AddSingleton<IInstalledReleaseMetadataProvider>(
            new FakeInstalledReleaseMetadataProvider(new InstalledReleaseMetadataDto("1.2.3", null, null, null, null)));

        // Act
        var cut = Render<LoginStatus>();

        // Assert
        var loginStatus = cut.Find(".login-status");
        Assert.NotNull(loginStatus.QuerySelector("a[href='/login']"));
        Assert.DoesNotContain("1.2.3", loginStatus.TextContent);
    }
}
