using Bunit;
using FinanceManager.Application;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web;
using FinanceManager.Web.Components;
using FinanceManager.Web.Services;
using FinanceManager.Web.Services.Updates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace FinanceManager.Tests.Components;

/// <summary>
/// Tests for the <see cref="LoginStatus"/> component: it shows the installed application version
/// (or a "Version unbekannt" fallback when no version is known) for an authenticated user without
/// ever exposing the user's raw id in the DOM, and shows a login link instead of any version/user
/// information for an unauthenticated visitor.
/// </summary>
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

    /// <summary>
    /// Registers the localization services that <see cref="LoginStatus"/> requires via its injected
    /// <see cref="IStringLocalizer{T}"/> of <see cref="Pages"/>.
    /// </summary>
    private void RegisterLocalization()
    {
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
        Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());
    }

    /// <summary>
    /// Runs <paramref name="action"/> under the given UI culture (per-thread, restored). The component
    /// resolves <c>Pages</c> resources against <see cref="CultureInfo.CurrentUICulture"/>, so assertions
    /// on localized text must pin the culture explicitly - the ambient culture differs between local
    /// dev machines (de) and CI runners (en-US).
    /// </summary>
    /// <param name="culture">The UI culture to apply while <paramref name="action"/> runs.</param>
    /// <param name="action">The test body to execute under the pinned culture.</param>
    private static void WithCulture(string culture, Action action)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>
    /// Verifies that an authenticated user with a known installed version sees that version number
    /// rendered in the login status.
    /// </summary>
    [Fact]
    public void RendersVersion_WhenAuthenticated_AndVersionAvailable()
    {
        // Arrange
        Services.AddSingleton<ICurrentUserService>(new FakeCurrentUserService());
        RegisterLocalization();
        Services.AddSingleton<IInstalledReleaseMetadataProvider>(
            new FakeInstalledReleaseMetadataProvider(new InstalledReleaseMetadataDto("1.2.3", null, null, null, null)));

        // Act
        var cut = Render<LoginStatus>();

        // Assert
        Assert.Contains("1.2.3", cut.Find(".login-status").TextContent);
    }

    /// <summary>
    /// Verifies that when the installed-release metadata provider reports no version, the component
    /// falls back to displaying the localized "Version unbekannt" text rather than an empty or broken
    /// version string. The UI culture is pinned to German because the resource lookup follows the
    /// ambient <see cref="CultureInfo.CurrentUICulture"/>, which is en-US on CI runners and would
    /// resolve the English fallback instead.
    /// </summary>
    [Fact]
    public void RendersFallback_WhenVersionIsNull()
    {
        WithCulture("de", () =>
        {
            // Arrange
            Services.AddSingleton<ICurrentUserService>(new FakeCurrentUserService());
            RegisterLocalization();
            Services.AddSingleton<IInstalledReleaseMetadataProvider>(
                new FakeInstalledReleaseMetadataProvider(new InstalledReleaseMetadataDto(null, null, null, null, null)));

            // Act
            var cut = Render<LoginStatus>();

            // Assert
            Assert.Contains("Version unbekannt", cut.Find(".login-status").TextContent);
        });
    }

    /// <summary>
    /// Verifies that the raw user id is never leaked into the login status markup - neither in the
    /// visible text nor in a <c>title</c> attribute - which would otherwise expose an internal
    /// identifier that has no meaning to the user and is unnecessary information to display.
    /// </summary>
    [Fact]
    public void DoesNotRenderUserId_WhenAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        Services.AddSingleton<ICurrentUserService>(new FakeCurrentUserService { UserId = userId });
        RegisterLocalization();
        Services.AddSingleton<IInstalledReleaseMetadataProvider>(
            new FakeInstalledReleaseMetadataProvider(new InstalledReleaseMetadataDto("1.2.3", null, null, null, null)));

        // Act
        var cut = Render<LoginStatus>();

        // Assert
        var loginStatus = cut.Find(".login-status");
        Assert.DoesNotContain(userId.ToString(), loginStatus.TextContent);
        Assert.Null(loginStatus.GetAttribute("title"));
    }

    /// <summary>
    /// Verifies that an unauthenticated visitor sees a link to the login page instead of any
    /// version information, since showing the app version to an unauthenticated user is not useful
    /// and login is the only relevant action available.
    /// </summary>
    [Fact]
    public void RendersLoginLink_WhenNotAuthenticated()
    {
        // Arrange
        Services.AddSingleton<ICurrentUserService>(new FakeCurrentUserService { IsAuthenticated = false });
        RegisterLocalization();
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
