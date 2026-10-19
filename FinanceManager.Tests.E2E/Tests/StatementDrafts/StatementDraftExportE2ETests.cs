using FinanceManager.Domain.Statements;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end tests for exporting the entries of a statement draft from the draft card page:
/// clicking the ribbon actions <c>ExportCsv</c>/<c>ExportXlsx</c> triggers a browser download of the
/// generated CSV/XLSX file containing the draft's entries.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class StatementDraftExportE2ETests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatementDraftExportE2ETests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public StatementDraftExportE2ETests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies the happy path for the CSV export: a seeded draft with entries is opened in the card view,
    /// the <c>ExportCsv</c> ribbon button triggers a download whose file name ends with <c>.csv</c> and whose
    /// content contains the CSV header line and the seeded entry values.
    /// </summary>
    [Fact]
    public async Task ExportCsv_ViaRibbon_ShouldDownloadCsvWithEntries()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"export-csv-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var draft = await SeedDraftWithEntriesAsync(user.Id);

        await page.GotoAsync($"/card/statement-drafts/{draft.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var csvButton = page.Locator("button#ExportCsv");
        await csvButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        (await csvButton.IsEnabledAsync()).Should().BeTrue();

        var download = await page.RunAndWaitForDownloadAsync(() => csvButton.ClickAsync());

        download.SuggestedFilename.Should().EndWith(".csv");
        download.SuggestedFilename.Should().StartWith("StatementDraft_");
        var path = await download.PathAsync();
        var content = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        content.Should().Contain("EntryNumber;BookingDate;ValutaDate;Amount;Currency;Subject");
        content.Should().Contain("\"Alpha Export\"");
        content.Should().Contain("\"Beta Export\"");
    }

    /// <summary>
    /// Verifies the happy path for the Excel export: the <c>ExportXlsx</c> ribbon button is visible and
    /// enabled on a loaded draft card and triggers a download whose file name ends with <c>.xlsx</c> and
    /// whose content starts with the ZIP magic bytes <c>PK</c> of the xlsx container format.
    /// </summary>
    [Fact]
    public async Task ExportXlsx_ViaRibbon_ShouldDownloadXlsx()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"export-xlsx-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var draft = await SeedDraftWithEntriesAsync(user.Id);

        await page.GotoAsync($"/card/statement-drafts/{draft.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var xlsxButton = page.Locator("button#ExportXlsx");
        await xlsxButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        (await xlsxButton.IsEnabledAsync()).Should().BeTrue();

        var download = await page.RunAndWaitForDownloadAsync(() => xlsxButton.ClickAsync());

        download.SuggestedFilename.Should().EndWith(".xlsx");
        download.SuggestedFilename.Should().StartWith("StatementDraft_");
        var path = await download.PathAsync();
        var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        bytes.Length.Should().BeGreaterThan(2);
        bytes[0].Should().Be((byte)'P');
        bytes[1].Should().Be((byte)'K');
    }

    private async Task<StatementDraft> SeedDraftWithEntriesAsync(Guid userId)
    {
        await using var db = CreateDbContext();
        var draft = new StatementDraft(userId, "export-e2e.csv", null, "Export E2E");
        draft.AddEntry(new DateTime(2026, 1, 5), -42.5m, "Alpha Export", "Shop A", new DateTime(2026, 1, 6), "EUR", null, false);
        draft.AddEntry(new DateTime(2026, 1, 10), 17m, "Beta Export", null, null, null, null, false);
        db.StatementDrafts.Add(draft);
        db.StatementDraftEntries.AddRange(draft.Entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return draft;
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_fixture.DatabasePath}")
            .Options;
        return new AppDbContext(options);
    }
}
