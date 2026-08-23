using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FinanceManager.Shared.Dtos.Statements;
using Microsoft.Playwright;

namespace FinanceManager.Tests.E2E;

[Collection(PlaywrightCollection.CollectionName)]
public sealed class StatementDraftQuickEditValueTakeoverE2ETests
{
    private readonly PlaywrightWebAppFixture _fixture;

    public StatementDraftQuickEditValueTakeoverE2ETests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task QuickEdit_PressesF8_ShouldCopySingleFieldFromRowAbove()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"f8-e2e-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E F8 Takeover", "DE50700500000007882904");

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
                        ["Amount"] = 123.45m,
                        ["Subject"] = "Source Subject",
                        ["RecipientName"] = "Source Recipient",
                        ["BookingDescription"] = "Source Description"
                    }
                }
            },
            creates = new object[] { },
            deletes = System.Array.Empty<Guid>()
        });

        await page.GotoAsync($"{_fixture.BaseUrl}/card/statement-drafts/{prelimDraft.DraftId}?quickEdit=true");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.Locator("input[id^='qe_booking_']").Nth(1).WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        var sourceSubject = page.Locator("input[id^='qe_subject_']").Nth(0);
        var targetSubject = page.Locator("input[id^='qe_subject_']").Nth(1);

        var expected = await sourceSubject.InputValueAsync();
        var targetHandle = await targetSubject.ElementHandleAsync();
        await page.EvaluateAsync("args => { const el = args[0]; const init = args[1]; el.dispatchEvent(new KeyboardEvent('keydown', init)); }", new object[]
        {
            targetHandle,
            new { key = "F8", code = "F8", ctrlKey = false, bubbles = true }
        });

        await page.WaitForFunctionAsync($"() => {{ const el = document.querySelectorAll('input[id^=\"qe_subject_\"]')[1]; return el && el.value === '{expected}'; }}");

        var actual = await targetSubject.InputValueAsync();
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task QuickEdit_PressesCtrlF8_ShouldCopyAllFieldsFromRowAboveAndOverwriteExisting()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"ctrlf8-e2e-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E Ctrl+F8 Takeover", "DE50700500000007882905");

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
                        ["Amount"] = 123.45m,
                        ["Subject"] = "Source Subject",
                        ["RecipientName"] = "Source Recipient",
                        ["BookingDescription"] = "Source Description"
                    }
                }
            },
            creates = new object[] { },
            deletes = System.Array.Empty<Guid>()
        });

        await page.GotoAsync($"{_fixture.BaseUrl}/card/statement-drafts/{prelimDraft.DraftId}?quickEdit=true");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.Locator("input[id^='qe_booking_']").Nth(1).WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        var sourceBookingDate = page.Locator("input[id^='qe_booking_']").Nth(0);
        var sourceValuta = page.Locator("input[id^='qe_valuta_']").Nth(0);
        var sourceAmount = page.Locator("input[id^='qe_amount_']").Nth(0);
        var sourceDescription = page.Locator("input[id^='qe_description_']").Nth(0);
        var sourceRecipient = page.Locator("input[id^='qe_recipient_']").Nth(0);
        var sourceSubject = page.Locator("input[id^='qe_subject_']").Nth(0);

        var targetBookingDate = page.Locator("input[id^='qe_booking_']").Nth(1);
        var targetValuta = page.Locator("input[id^='qe_valuta_']").Nth(1);
        var targetAmount = page.Locator("input[id^='qe_amount_']").Nth(1);
        var targetDescription = page.Locator("input[id^='qe_description_']").Nth(1);
        var targetRecipient = page.Locator("input[id^='qe_recipient_']").Nth(1);
        var targetSubject = page.Locator("input[id^='qe_subject_']").Nth(1);

        // Pre-fill the target row with different values to verify overwrite
        await targetBookingDate.FillAsync("2020-01-01");
        await targetAmount.FillAsync("0.01");
        await targetDescription.FillAsync("Different");
        await targetRecipient.FillAsync("Different");
        await targetSubject.FillAsync("Different");

        var expectedSubject = await sourceSubject.InputValueAsync();
        var targetSubjectHandle = await targetSubject.ElementHandleAsync();
        await page.EvaluateAsync("args => { const el = args[0]; const init = args[1]; el.dispatchEvent(new KeyboardEvent('keydown', init)); }", new object[]
        {
            targetSubjectHandle,
            new { key = "F8", code = "F8", ctrlKey = true, bubbles = true }
        });

        await page.WaitForFunctionAsync($"() => {{ const el = document.querySelectorAll('input[id^=\"qe_subject_\"]')[1]; return el && el.value === '{expectedSubject}'; }}");

        (await targetBookingDate.InputValueAsync()).Should().Be(await sourceBookingDate.InputValueAsync());
        (await targetAmount.InputValueAsync()).Should().Be(await sourceAmount.InputValueAsync());
        (await targetDescription.InputValueAsync()).Should().Be(await sourceDescription.InputValueAsync());
        (await targetRecipient.InputValueAsync()).Should().Be(await sourceRecipient.InputValueAsync());
        (await targetSubject.InputValueAsync()).Should().Be(await sourceSubject.InputValueAsync());

        var sourceValutaValue = await sourceValuta.InputValueAsync();
        if (string.IsNullOrWhiteSpace(sourceValutaValue))
        {
            (await targetValuta.InputValueAsync()).Should().BeEmpty();
        }
        else
        {
            (await targetValuta.InputValueAsync()).Should().Be(sourceValutaValue);
        }
    }
}
