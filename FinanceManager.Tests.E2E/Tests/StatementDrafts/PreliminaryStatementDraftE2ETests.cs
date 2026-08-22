using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FinanceManager.Domain.Statements;
using FinanceManager.Infrastructure;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Statements;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.E2E;

[Collection(PlaywrightCollection.CollectionName)]
public sealed class PreliminaryStatementDraftE2ETests
{
    private readonly PlaywrightWebAppFixture _fixture;

    public PreliminaryStatementDraftE2ETests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BookPreliminaryDraft_ShouldCreatePreliminaryPostings()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"prelim-e2e-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E Prelim", "DE50700500000007882900");
        var contact = await BrowserApiHelper.PostJsonAsync<ContactCreateRequest, ContactDto>(
            page,
            "/api/contacts",
            new ContactCreateRequest("E2E Shop", ContactType.Organization, null, null, false));

        var prelimDraft = await BrowserApiHelper.PostJsonAsync<CreatePreliminaryStatementDraftRequest, StatementDraftDto>(
            page,
            "/api/statement-drafts/preliminary",
            new CreatePreliminaryStatementDraftRequest(account.Id));

        var entryId = prelimDraft.Entries!.Single().Id;

        await BrowserApiHelper.PostJsonAsync(page, $"/api/statement-drafts/{prelimDraft.DraftId}/entries/batch-update", new
        {
            updates = new[]
            {
                new
                {
                    entryId,
                    fields = new Dictionary<string, object?>
                    {
                        ["BookingDate"] = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ["Amount"] = 100m,
                        ["Subject"] = "Preliminary",
                        ["RecipientName"] = "E2E Shop",
                        ["BookingDescription"] = "Preliminary booking"
                    }
                }
            },
            creates = new object[] { },
            deletes = System.Array.Empty<Guid>()
        });

        await BrowserApiHelper.PostJsonAsync(page, $"/api/statement-drafts/{prelimDraft.DraftId}/entries/{entryId}/contact", new StatementDraftSetContactRequest(contact.Id));
        await BrowserApiHelper.PostNoContentAsync(page, $"/api/statement-drafts/{prelimDraft.DraftId}/book?forceWarnings=false");

        var postings = await BrowserApiHelper.GetJsonAsync<List<PostingServiceDto>>(page, $"/api/postings/account/{account.Id}");
        postings.Count.Should().Be(1);
        postings.Should().OnlyContain(p => p.IsPreliminary);

        await page.GotoAsync($"/list/postings/account/{account.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var preliminaryCheckCells = page.Locator("td:has-text('✓')");
        (await preliminaryCheckCells.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task BookRealStatement_ShouldReversePreliminaryPostings()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"real-e2e-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E Real", "DE50700500000007882901");
        var contact = await BrowserApiHelper.PostJsonAsync<ContactCreateRequest, ContactDto>(
            page,
            "/api/contacts",
            new ContactCreateRequest("E2E Shop", ContactType.Organization, null, null, false));

        // 1. Create and book a preliminary draft
        var prelimDraft = await BrowserApiHelper.PostJsonAsync<CreatePreliminaryStatementDraftRequest, StatementDraftDto>(
            page,
            "/api/statement-drafts/preliminary",
            new CreatePreliminaryStatementDraftRequest(account.Id));

        var prelimEntryId = prelimDraft.Entries!.Single().Id;
        await BrowserApiHelper.PostJsonAsync(page, $"/api/statement-drafts/{prelimDraft.DraftId}/entries/batch-update", new
        {
            updates = new[]
            {
                new
                {
                    entryId = prelimEntryId,
                    fields = new Dictionary<string, object?>
                    {
                        ["BookingDate"] = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ["Amount"] = 100m,
                        ["Subject"] = "Preliminary",
                        ["RecipientName"] = "E2E Shop",
                        ["BookingDescription"] = "Preliminary booking"
                    }
                }
            },
            creates = new object[] { },
            deletes = System.Array.Empty<Guid>()
        });
        await BrowserApiHelper.PostJsonAsync(page, $"/api/statement-drafts/{prelimDraft.DraftId}/entries/{prelimEntryId}/contact", new StatementDraftSetContactRequest(contact.Id));
        await BrowserApiHelper.PostNoContentAsync(page, $"/api/statement-drafts/{prelimDraft.DraftId}/book?forceWarnings=false");

        // 2. Seed a real (non-preliminary) statement draft directly in the database
        await using var db = CreateDbContext();
        var realDraft = new StatementDraft(user.Id, "real-statement.csv", "", "Real statement");
        realDraft.SetDetectedAccount(account.Id);
        var realEntry = realDraft.AddEntry(DateTime.Today, 50m, "Real", "E2E Shop", DateTime.Today, null, null, false, false);
        db.StatementDrafts.Add(realDraft);
        db.StatementDraftEntries.Add(realEntry);
        await db.SaveChangesAsync();

        await BrowserApiHelper.PostJsonAsync(page, $"/api/statement-drafts/{realDraft.Id}/entries/{realEntry.Id}/contact", new StatementDraftSetContactRequest(contact.Id));

        var bookResponse = await BrowserApiHelper.PostWithStatusAsync<BookingResult>(page, $"/api/statement-drafts/{realDraft.Id}/book?forceWarnings=true");
        bookResponse.Status.Should().Be(200);
        bookResponse.Value.Should().NotBeNull();
        bookResponse.Value!.Success.Should().BeTrue();

        // 3. Verify reversal and the new real postings
        var postings = await BrowserApiHelper.GetJsonAsync<List<PostingServiceDto>>(page, $"/api/postings/account/{account.Id}");

        var preliminaryPostings = postings.Where(p => p.Subject == "Preliminary" && p.IsPreliminary).ToList();
        preliminaryPostings.Count.Should().Be(1);
        preliminaryPostings.Should().OnlyContain(p => p.IsReversed && p.Amount == 0m);

        var realPostings = postings.Where(p => p.Subject == "Real" && !p.IsPreliminary).ToList();
        realPostings.Count.Should().Be(1);
        realPostings.Should().OnlyContain(p => !p.IsReversed);

        await page.GotoAsync($"/list/postings/account/{account.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.Locator("text=Preliminary").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
    }

    [Fact]
    public async Task CreatePreliminaryDraft_ViaRibbon_ShouldCreateAndOpenDraftWithQuickEdit()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"prelim-ribbon-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E Prelim Ribbon", "DE50700500000007882902");

        await page.GotoAsync($"/card/accounts/{account.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var button = page.Locator("button#CreatePreliminaryDraft");
        await button.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await button.ClickAsync();

        await page.WaitForURLAsync(url => url.Contains("/card/statement-drafts/") && url.Contains("quickEdit=true"), new() { Timeout = 15000 });
        page.Url.Should().Contain("/card/statement-drafts/");
        page.Url.Should().Contain("quickEdit=true");

        await page.WaitForFunctionAsync("() => { const a = document.activeElement; return a && a.id && a.id.startsWith('qe_booking_'); }", null, new() { Timeout = 15000 });

        var activeElementId = await page.EvaluateAsync<string?>("() => document.activeElement?.id");
        activeElementId.Should().StartWith("qe_booking_");
    }

    [Fact]
    public async Task OpenPreliminaryDraft_Card_ShouldShowPreliminaryIndicator()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"prelim-indicator-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E Prelim Indicator", "DE50700500000007882903");

        var prelimDraft = await BrowserApiHelper.PostJsonAsync<CreatePreliminaryStatementDraftRequest, StatementDraftDto>(
            page,
            "/api/statement-drafts/preliminary",
            new CreatePreliminaryStatementDraftRequest(account.Id));

        await page.GotoAsync($"/card/statement-drafts/{prelimDraft.DraftId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The localized label is shown for preliminary drafts ("Vorläufig" in German, "Preliminary" in English).
        var indicator = page.Locator("th:has-text('Preliminary'), th:has-text('Vorläufig')").First;
        await indicator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        var value = page.Locator("td:has-text('Yes'), td:has-text('Ja')").First;
        await value.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_fixture.DatabasePath}")
            .Options;
        return new AppDbContext(options);
    }
}
