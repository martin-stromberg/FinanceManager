using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Bunit;
using FluentAssertions;
using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Admin;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Shared.Dtos.Users;
using FinanceManager.Web;
using FinanceManager.Web.Components.Pages;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.Services;
using FinanceManager.Web.ViewModels.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using msTools.Web.Blazor;

namespace FinanceManager.Tests.Components;

/// <summary>
/// Verifies the dirty-state detection of the setup card: every visible input field of every
/// dirty-aggregated setup section must enable the global "Save" ribbon button when changed and
/// disable it again when the previously saved value is restored. Fields are enumerated
/// dynamically from the rendered markup so newly added input fields are covered automatically.
/// Sections without dirty aggregation must be listed explicitly in <see cref="ExcludedSections"/>;
/// a new section that is neither aggregated nor excluded fails <see cref="SettingSections_AreAllClassified"/>.
/// </summary>
public sealed class SetupSectionsDirtyDetectionTests : BunitContext
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    // Sections whose view models contribute to SetupCardViewModel.HasPendingChanges.
    private static readonly HashSet<string> DirtyAggregatedSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "profile", "notifications", "statements", "update", "returnanalysis", "securitytxt", "wellknown"
    };

    // Sections with their own save semantics that are not part of the global Save button.
    private static readonly HashSet<string> ExcludedSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "attachments", "backup", "security"
    };

    /// <summary>
    /// Ensures every section offered by the setup card is classified: either it contributes to
    /// <c>HasPendingChanges</c> (its fields must toggle Save) or it is explicitly excluded.
    /// A newly added section fails this test until it is classified here, preventing silently
    /// incomplete coverage of the settings page.
    /// </summary>
    [Fact]
    public async Task SettingSections_AreAllClassified()
    {
        var sp = BuildServices(isAdmin: true);
        var vm = new SetupCardViewModel(sp);
        await vm.LoadAsync(Guid.Empty);

        var keys = vm.SettingSections.Select(s => s.Key).ToList();
        var unclassified = keys
            .Where(k => !DirtyAggregatedSections.Contains(k) && !ExcludedSections.Contains(k))
            .ToList();

        unclassified.Should().BeEmpty(
            "every setup section must be classified as dirty-aggregated or explicitly excluded: {0}",
            string.Join(", ", unclassified));
    }

    /// <summary>
    /// For each dirty-aggregated section: expands the section, then for every rendered visible
    /// input/select element changes the value and expects the ribbon "Save" button to become
    /// enabled; restoring the saved value must disable it again.
    /// </summary>
    /// <param name="sectionKey">The setup section key to test (e.g. "statements").</param>
    [Theory]
    [InlineData("profile")]
    [InlineData("notifications")]
    [InlineData("statements")]
    [InlineData("update")]
    [InlineData("returnanalysis")]
    [InlineData("securitytxt")]
    [InlineData("wellknown")]
    public async Task Section_EachVisibleField_TogglesSaveButton(string sectionKey)
    {
        DirtyAggregatedSections.Should().Contain(sectionKey);

        JSInterop.Mode = JSRuntimeMode.Loose;
        var sp = BuildServices(isAdmin: true);
        var vm = new SetupCardViewModel(sp);
        await vm.LoadAsync(Guid.Empty);

        var sections = Render<SetupSections>(parameters => parameters.Add(p => p.Provider, vm));
        var ribbon = Render<Ribbon<string>>(parameters => parameters
            .Add(p => p.Provider, vm)
            .Add(p => p.Localizer, sp.GetRequiredService<IStringLocalizer<Pages>>()));

        var sectionIndex = vm.SettingSections
            .Select((s, i) => (s.Key, i))
            .First(t => string.Equals(t.Key, sectionKey, StringComparison.OrdinalIgnoreCase)).i;

        // Re-query DOM elements on every access: bUnit re-renders may replace nodes.
        IElement SectionItem() => sections.FindAll("section.setup-section-accordion-item")[sectionIndex];
        IReadOnlyList<IElement> GetFields()
            => SectionItem().QuerySelectorAll(".setup-section-content input:not([type=hidden]), .setup-section-content select")
                .Where(IsEditableField)
                .ToList();

        SectionItem().QuerySelector("button.setup-section-toggle")!.Click();

        // WaitForState polls independently of renders: the tab re-renders itself after LoadAsync
        // without triggering a render of the surrounding SetupSections component.
        sections.WaitForState(() => GetFields().Count > 0, timeout: TimeSpan.FromSeconds(10));
        GetFields().Should().NotBeEmpty(
            $"section '{sectionKey}' must render at least one editable field (content: {SectionItem().TextContent})");

        var fieldCount = GetFields().Count;
        var failures = new List<string>();
        for (var fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
        {
            var field = GetFields()[fieldIndex];
            var name = FieldName(field);
            var original = CurrentValue(field);
            var alternative = AlternativeValue(field, original);
            if (alternative is null)
            {
                failures.Add($"[{sectionKey}] field '{name}': no alternative value available (e.g. select without options)");
                continue;
            }

            // Act: change the field value.
            field.Change(alternative);
            ribbon.Render();
            var enabledAfterChange = !IsSaveDisabled(ribbon);
            var pendingAfterChange = vm.HasPendingChanges;

            // Revert to the saved value (re-query: the element may have been re-rendered).
            GetFields()[fieldIndex].Change(original!);
            ribbon.Render();
            var disabledAfterRevert = IsSaveDisabled(ribbon);
            var pendingAfterRevert = vm.HasPendingChanges;

            if (!enabledAfterChange)
            {
                failures.Add($"[{sectionKey}] field '{name}': change '{original}' -> '{alternative}' did not enable Save (HasPendingChanges={pendingAfterChange})");
            }
            else if (!disabledAfterRevert)
            {
                failures.Add($"[{sectionKey}] field '{name}': restoring saved value '{original}' did not disable Save again (HasPendingChanges={pendingAfterRevert})");
            }
        }

        failures.Should().BeEmpty("every field must toggle the Save button:\n" + string.Join("\n", failures));
    }

    private static bool IsEditableField(IElement el)
    {
        if (el is IHtmlInputElement input && input.IsDisabled)
        {
            return false;
        }
        if (el is IHtmlSelectElement select && select.IsDisabled)
        {
            return false;
        }
        for (var node = el; node is not null; node = node.ParentElement)
        {
            if (node.HasAttribute("hidden"))
            {
                return false;
            }
        }
        return true;
    }

    private static string FieldName(IElement el)
        => el.Id is { Length: > 0 } id
            ? id
            : el.Closest(".form-row")?.QuerySelector("label")?.TextContent.Trim()
              ?? el.GetAttribute("name") ?? el.TagName;

    private static object? CurrentValue(IElement el) => el switch
    {
        IHtmlInputElement { Type: "checkbox" } input => input.IsChecked,
        IHtmlInputElement input => input.Value,
        IHtmlSelectElement select => select.Value,
        _ => el.GetAttribute("value")
    };

    private static object? AlternativeValue(IElement el, object? current)
    {
        switch (el)
        {
            case IHtmlInputElement { Type: "checkbox" }:
                return current is not true;
            case IHtmlInputElement input when input.Type == "number":
                return decimal.TryParse(current?.ToString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var n) ? (n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : "1";
            case IHtmlInputElement input when input.Type == "time":
                return Equals(current, "13:47") ? "14:48" : "13:47";
            case IHtmlInputElement input when input.Type == "datetime-local":
                return Equals(current, "2030-01-01T13:45") ? "2030-01-02T14:45" : "2030-01-01T13:45";
            case IHtmlInputElement:
                return string.IsNullOrEmpty(current?.ToString()) ? "x" : current + "x";
            case IHtmlSelectElement select:
                return select.Options
                    .Select(o => o.GetAttribute("value") ?? o.Value)
                    .FirstOrDefault(v => !Equals(v, current));
            default:
                return null;
        }
    }

    private static bool IsSaveDisabled(IRenderedComponent<Ribbon<string>> ribbon)
        => ribbon.Find("#Save").HasAttribute("disabled");

    private IServiceProvider BuildServices(bool isAdmin)
    {
        var apiMock = new Mock<IApiClient>();

        apiMock.Setup(a => a.UserSettings_GetProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfileSettingsDto { PreferredLanguage = "de", TimeZoneId = "Europe/Berlin" });
        apiMock.Setup(a => a.User_GetNotificationSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettingsDto { HolidayProvider = "Memory" });
        apiMock.Setup(a => a.UserSettings_GetImportSplitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportSplitSettingsDto
            {
                Mode = ImportSplitMode.MonthlyOrFixed,
                MaxEntriesPerDraft = 250,
                MonthlySplitThreshold = 250,
                MinEntriesPerDraft = 8
            });
        apiMock.Setup(a => a.Securities_ListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityDto>
            {
                new() { Id = Guid.NewGuid(), Name = "Benchmark ETF", IsActive = true },
                new() { Id = Guid.NewGuid(), Name = "Other Security", IsActive = true }
            });
        apiMock.Setup(a => a.Securities_GetReturnAnalysisSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReturnAnalysisSettingsResponse(null, null, false, 0m));
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false));
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateStatusDto(UpdateStatusKind.NoUpdate, "1.0.0", null, null, "win-x64", null, null, null, false, null, null, null));
        apiMock.Setup(a => a.GetSecurityTxtSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityTxtSettingsDto { Contact = "mailto:sec@example.org", Expires = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2027, 1, 1))) });
        apiMock.Setup(a => a.GetWellKnownSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WellKnownSettingsDto { ChangePasswordUrl = "/change-password" });

        Services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAdmin = isAdmin });
        Services.AddLogging();
        Services.AddSingleton<IKpiLocalStorageCache>(Mock.Of<IKpiLocalStorageCache>());
        Services.AddSingleton(Mock.Of<IConfirmationService>());
        Services.AddHttpClient();
        Services.AddSingleton(apiMock.Object);
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
        Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());
        Services.AddScoped<LoadingBarService>();
        return Services.BuildServiceProvider();
    }
}
