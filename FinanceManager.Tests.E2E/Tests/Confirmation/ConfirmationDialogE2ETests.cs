using FinanceManager.Domain.Attachments;
using FinanceManager.Shared.Dtos.Attachments;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end coverage for the unified confirmation dialog on destructive actions.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class ConfirmationDialogE2ETests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationDialogE2ETests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture.</param>
    public ConfirmationDialogE2ETests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Clicking the ribbon Delete button on an account card opens the confirmation dialog.
    /// Cancelling keeps the account; confirming deletes it and navigates back to the list.
    /// </summary>
    [Fact]
    public async Task AccountDelete_RibbonAction_ShowsConfirmationAndDeletesOnConfirm()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var user = await LoginNewUserAsync(page, "confirmation-delete");
        var seed = new AccountsApiSeedHelper(page, _fixture.DatabasePath, user.Id);

        var unique = Guid.NewGuid().ToString("N");
        var accountName = $"Delete Me {unique}";
        var account = await seed.CreateAccountAsync(accountName, $"DE991001001000000{unique[..8]}");

        await page.GotoAsync($"/card/accounts/{account.Id}");
        await page.Locator(".card-view").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        var deleteButton = page.Locator("#Delete");
        await deleteButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        // 1. Cancel the delete: dialog should disappear and the account should still exist.
        await deleteButton.ClickAsync();
        var dialog = page.Locator(".confirm-dialog");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        await Microsoft.Playwright.Assertions.Expect(dialog.Locator("#confirm-title")).ToBeVisibleAsync();

        await dialog.Locator(".confirm-dialog-actions button.secondary").ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 30000 });

        var currentUrl = page.Url;
        currentUrl.Should().Contain($"/card/accounts/{account.Id}");

        // 2. Confirm the delete: dialog should close and the app should navigate away from the card.
        await deleteButton.ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        await dialog.Locator(".confirm-dialog-actions button:not(.secondary)").ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 30000 });

        await page.WaitForURLAsync("**/list/accounts");

        // The account should no longer appear in the list.
        var list = new ListPageGateway(page);
        await list.OpenAccountsExpectingEmptyAsync();
        var matchingRows = await list.CountVisibleRowsAsync(accountName);
        matchingRows.Should().Be(0, $"deleted account '{accountName}' should not appear in the list");
    }

    /// <summary>
    /// Deleting an attachment while the attachments overlay is open shows the confirmation dialog above
    /// the overlay; confirming deletes the attachment and keeps the overlay open.
    /// </summary>
    [Fact]
    public async Task AttachmentDelete_OverlayOpen_ShowsConfirmationAboveOverlayAndDeletes()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;

        var unique = Guid.NewGuid().ToString("N");
        var deleteFileName = $"delete-{unique}.txt";
        var keepFileName = $"keep-{unique}.txt";
        var (overlayDialog, dialog, deleteRow) = await OpenConfirmDialogForAttachmentDeleteAsync(page, "attachment-delete", deleteFileName, keepFileName);

        // Playwright's hit-target check fails when the dialog is covered by the overlay.
        await dialog.Locator(".confirm-dialog-actions button:not(.secondary)").ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 30000 });

        await deleteRow.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 30000 });
        await Microsoft.Playwright.Assertions.Expect(overlayDialog).ToBeVisibleAsync();
        await Microsoft.Playwright.Assertions.Expect(overlayDialog.Locator("tbody tr", new() { HasText = keepFileName })).ToBeVisibleAsync();
    }

    /// <summary>
    /// Cancelling the confirmation dialog above an open attachments overlay keeps both the attachment
    /// and the overlay.
    /// </summary>
    [Fact]
    public async Task AttachmentDelete_OverlayOpen_CancelKeepsAttachmentAndOverlay()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;

        var fileName = $"keep-{Guid.NewGuid():N}.txt";
        var (overlayDialog, dialog, row) = await OpenConfirmDialogForAttachmentDeleteAsync(page, "attachment-cancel", fileName);

        await dialog.Locator(".confirm-dialog-actions button.secondary").ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 30000 });

        await Microsoft.Playwright.Assertions.Expect(overlayDialog).ToBeVisibleAsync();
        await Microsoft.Playwright.Assertions.Expect(row).ToBeVisibleAsync();
    }

    /// <summary>
    /// Clicking the confirmation dialog's backdrop cancels only the confirmation; the attachments
    /// overlay underneath stays open and the attachment remains.
    /// </summary>
    [Fact]
    public async Task AttachmentDelete_OverlayOpen_BackdropClickCancelsOnlyConfirmation()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;

        var fileName = $"keep-{Guid.NewGuid():N}.txt";
        var (overlayDialog, dialog, row) = await OpenConfirmDialogForAttachmentDeleteAsync(page, "attachment-backdrop", fileName);

        // Click the top-left corner of the confirmation backdrop, outside the dialog itself.
        var backdrop = page.Locator(".split-center.confirm-dialog-layer");
        await backdrop.ClickAsync(new() { Position = new() { X = 4, Y = 4 } });

        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 30000 });
        await Microsoft.Playwright.Assertions.Expect(overlayDialog).ToBeVisibleAsync();
        await Microsoft.Playwright.Assertions.Expect(row).ToBeVisibleAsync();
    }

    private async Task<(ILocator Overlay, ILocator Dialog, ILocator Row)> OpenConfirmDialogForAttachmentDeleteAsync(
        IPage page, string userPrefix, string fileName, params string[] additionalFileNames)
    {
        var user = await LoginNewUserAsync(page, userPrefix);
        var seed = new AccountsApiSeedHelper(page, _fixture.DatabasePath, user.Id);

        var contactId = await seed.CreateBankContactAsync($"Attachment Contact {Guid.NewGuid():N}");
        var uploadPath = $"/api/attachments/{(short)AttachmentEntityKind.Contact}/{contactId}";
        await BrowserApiHelper.PostMultipartAsync<AttachmentDto>(page, uploadPath, fileName, "text/plain", Encoding.UTF8.GetBytes(fileName));
        foreach (var additionalFileName in additionalFileNames)
        {
            await BrowserApiHelper.PostMultipartAsync<AttachmentDto>(page, uploadPath, additionalFileName, "text/plain", Encoding.UTF8.GetBytes(additionalFileName));
        }

        var overlay = await OpenAttachmentsOverlayAsync(page, contactId);

        var row = overlay.Locator("tbody tr", new() { HasText = fileName });
        await row.Locator("button.icon-btn.danger").ClickAsync();

        var dialog = page.Locator(".confirm-dialog");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        return (overlay, dialog, row);
    }

    private static async Task<ILocator> OpenAttachmentsOverlayAsync(IPage page, Guid contactId)
    {
        await page.GotoAsync($"/card/contacts/{contactId}");
        await page.Locator(".card-view").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        var attachmentsButton = page.Locator("#Attachments");
        await attachmentsButton.ClickAsync();

        var overlayDialog = page.Locator(".split-dialog:not(.confirm-dialog)");
        await overlayDialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await overlayDialog.Locator("button.icon-btn.danger").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        return overlayDialog;
    }

    private async Task<FinanceManager.Domain.Users.User> LoginNewUserAsync(IPage page, string prefix)
    {
        var username = $"{prefix}-{Guid.NewGuid():N}";
        return await LoginExistingUserAsync(page, username, "Secret123");
    }

    private async Task<FinanceManager.Domain.Users.User> LoginExistingUserAsync(IPage page, string username, string password)
    {
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);
        var user = await seeder.EnsureUserAsync(username, password, timeZoneId: "UTC");
        await auth.LoginAsync(username, password);
        return user;
    }
}
