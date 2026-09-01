using Bunit;
using FluentAssertions;
using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web;
using FinanceManager.Web.Components.Pages;
using FinanceManager.Web.Components.Shared;
using msTools.Web.Blazor;
using FinanceManager.Web.Services;
using FinanceManager.Web.ViewModels.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Components;

/// <summary>
/// Tests for the <see cref="SetupSections"/> component and its interaction with the setup ribbon,
/// covering the "backup" section's upload-backup workflow.
/// </summary>
public sealed class SetupSectionsTests : BunitContext
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    /// <summary>
    /// Verifies that clicking the "UploadBackup" ribbon button raises the backup section
    /// view-model's <c>UploadRequested</c> event, confirming the ribbon action set up for the
    /// backup section is actually wired through to the view-model that owns the upload workflow,
    /// rather than the button being present but disconnected.
    /// </summary>
    [Fact]
    public async Task UploadBackup_RaisesUploadRequested_AfterSectionExpand()
    {
        // Arrange
        Services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        Services.AddSingleton<IApiClient>(new Mock<IApiClient>().Object);
        Services.AddSingleton<IKpiLocalStorageCache>(Mock.Of<IKpiLocalStorageCache>());
        Services.AddScoped<LoadingBarService>();
        JSInterop.SetupVoid("financeManager.loadingBar.start").SetVoidResult();
        JSInterop.SetupVoid("financeManager.loadingBar.stop").SetVoidResult();
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
        Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

        var sp = Services.BuildServiceProvider();
        var vm = new SetupCardViewModel(sp);
        await vm.LoadAsync(Guid.Empty);

        var backupVm = vm.CreateSectionViewModel("backup", sp);
        backupVm.Should().BeOfType<SetupBackupsViewModel>();

        var uploadRequested = false;
        ((SetupBackupsViewModel)backupVm!).UploadRequested += (_, _) => uploadRequested = true;

        Render<SetupSections>(parameters => parameters.Add(p => p.Provider, vm));
        var ribbon = Render<Ribbon<string>>(parameters => parameters
            .Add(p => p.Provider, vm)
            .Add(p => p.Localizer, sp.GetRequiredService<IStringLocalizer<Pages>>()));

        // Act
        ribbon.Find("#UploadBackup").Click();

        // Assert
        ribbon.WaitForAssertion(() => Assert.True(uploadRequested));
    }
}
