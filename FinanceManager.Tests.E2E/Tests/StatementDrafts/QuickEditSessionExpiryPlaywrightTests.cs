using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace FinanceManager.Tests.E2E;

/// <summary>Checks that real typing keeps both existing and new unsaved rows usable past JWT expiry.</summary>
/// <param name="fixture">The isolated regular application used for session scenarios.</param>
/// <returns>A test instance bound to the supplied application fixture.</returns>
[Collection(SessionExpiryCollection.Name)]
public sealed class QuickEditSessionExpiryPlaywrightTests(SessionExpiryWebAppFixture fixture)
{
    /// <summary>Preserves an existing row and its focused field while actual typing renews authentication.</summary>
    [Fact]
    public Task QuickEdit_ExistingRow_InputWithoutBlurSurvivesExpiry() => ExerciseRowAsync(false);

    /// <summary>Preserves a new UI placeholder row without saving or blurring during renewal.</summary>
    [Fact]
    public Task QuickEdit_NewPlaceholderRow_InputWithoutBlurSurvivesExpiry() => ExerciseRowAsync(true);

    /// <summary>Checks the setup and live-value reopening assertions before expensive expiry runs.</summary>
    /// <param name="newRow">Whether to exercise a new placeholder instead of an existing row.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task QuickEdit_SaveAndReopen_PreservesLiveInputValues(bool newRow)
        => ExerciseRowAsync(newRow, waitForExpiry: false);

    private async Task ExerciseRowAsync(bool newRow, bool waitForExpiry = true)
    {
        await using var session = await fixture.CreateSessionAsync();
        var page = session.Page;
        await InitializationVisibilityProbe.InstallAsync(page.Context);
        var probe = new SessionExpiryProbe(fixture, page);
        var login = await probe.LoginAsync(newRow ? "new-row" : "existing-row");
        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("Session quick edit", $"DE507005000000{Random.Shared.Next(10000000, 99999999)}");
        var draft = await BrowserApiHelper.PostJsonAsync<CreatePreliminaryStatementDraftRequest, StatementDraftDto>(page,
            "/api/statement-drafts/preliminary", new CreatePreliminaryStatementDraftRequest(account.Id));
        var existingId = draft.Entries!.Single().Id;
        await BrowserApiHelper.PostJsonAsync(page, $"/api/statement-drafts/{draft.DraftId}/entries/batch-update", new
        {
            updates = new[] { new { entryId = existingId, fields = new Dictionary<string, object?>
            {
                ["BookingDate"] = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["Amount"] = 12m, ["Subject"] = "Original", ["RecipientName"] = "Original recipient", ["BookingDescription"] = "Original description"
            } } },
            creates = Array.Empty<object>(),
            deletes = Array.Empty<Guid>()
        });
        await page.GotoAsync($"/card/statement-drafts/{draft.DraftId}?quickEdit=true");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var subject = newRow ? page.Locator("input[id^='qe_subject_']").Last : page.Locator($"#qe_subject_{existingId}");
        await subject.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        var id = (await subject.GetAttributeAsync("id"))!;
        subject = page.Locator($"#{id}");
        var rowId = Guid.Parse(id["qe_subject_".Length..]);
        var date = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await page.Locator($"#qe_booking_{rowId}").FillAsync(date);
        await page.Locator($"#qe_amount_{rowId}").FillAsync("45");
        await page.Locator($"#qe_recipient_{rowId}").FillAsync("Unsaved recipient");
        await page.Locator($"#qe_description_{rowId}").FillAsync("Unsaved description");
        await subject.FillAsync("Unsaved");
        await subject.FocusAsync();
        await page.EvaluateAsync("""
            id => {
                window.__sessionBlurCount = 0;
                document.getElementById(id).addEventListener('blur', () => window.__sessionBlurCount++);
            }
            """, id);
        var before = await probe.ReadBrowserTokenAsync();
        var expires = before.Expires > login.Expires ? before.Expires : login.Expires;
        var deadline = DateTimeOffset.UtcNow.AddMinutes(fixture.LifetimeMinutes + 3);
        var expected = "Unsaved";
        var successfulPings = 0;
        page.Response += (_, response) =>
        {
            if (new Uri(response.Url).AbsolutePath == "/api/auth/keepalive" && response.Status == 204)
                Interlocked.Increment(ref successfulPings);
        };
        while (waitForExpiry && DateTimeOffset.UtcNow <= expires.AddSeconds(12))
        {
            DateTimeOffset.UtcNow.Should().BeBefore(deadline);
            await subject.PressSequentiallyAsync("x");
            expected += "x";
            await Task.Delay(5000);
            (await page.EvaluateAsync<string>("() => document.activeElement.id")).Should().Be(id);
            (await page.EvaluateAsync<int>("() => window.__sessionBlurCount")).Should().Be(0);
        }
        await subject.PressSequentiallyAsync("z");
        expected += "z";
        (await subject.InputValueAsync()).Should().Be(expected);
        (await page.Locator($"#qe_booking_{rowId}").InputValueAsync()).Should().Be(date);
        (await page.Locator($"#qe_amount_{rowId}").InputValueAsync()).Should().Be("45");
        (await page.Locator($"#qe_recipient_{rowId}").InputValueAsync()).Should().Be("Unsaved recipient");
        (await page.Locator($"#qe_description_{rowId}").InputValueAsync()).Should().Be("Unsaved description");
        if (waitForExpiry)
        {
            successfulPings.Should().BeGreaterThan(0);
            (await probe.ReadBrowserTokenAsync()).Expires.Should().BeAfter(expires);
        }
        await using (var db = CreateContext())
        {
            var unsaved = await db.StatementDraftEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == rowId);
            if (newRow) unsaved.Should().BeNull();
            else unsaved!.Subject.Should().Be("Original");
        }
        await InitializationVisibilityProbe.AssertVisibleAsync(page);
        var sequence = fixture.ReadObservations().Max(e => e.Sequence);
        await page.Locator("button#SaveQuickEdit").ClickAsync();
        var saved = await probe.ExpectApiAsync($"/api/statement-drafts/{draft.DraftId}/entries/batch-update", sequence, login.UserId);
        saved.Status.Should().Be(200);
        await using (var db = CreateContext())
        {
            var persisted = await db.StatementDraftEntries.AsNoTracking().SingleAsync(e => e.DraftId == draft.DraftId && e.Subject == expected);
            persisted.Amount.Should().Be(45m);
            persisted.BookingDate.Date.Should().Be(DateTime.Today.AddDays(-1));
            persisted.RecipientName.Should().Be("Unsaved recipient");
            persisted.BookingDescription.Should().Be("Unsaved description");
        }
        await page.GotoAsync($"/card/statement-drafts/{draft.DraftId}?quickEdit=true");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForFunctionAsync("expected => Array.from(document.querySelectorAll('input[id^=qe_subject_]')).some(input => input.value === expected)", expected);
    }

    private AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={fixture.DatabasePath}").Options);
}
