namespace FinanceManager.Tests.E2E;

/// <summary>
/// Wraps the home page's statement-upload widget, in particular the "mass import" dialog that can appear
/// when the uploaded file matches more than one existing account. Tests use this instead of driving the
/// file input and dialog directly so they don't have to duplicate the (somewhat involved) two-path wait
/// logic for "import succeeded immediately" vs. "import needs a disambiguation click first".
/// </summary>
public sealed class HomePageGateway
{
    private readonly IPage _page;

    /// <summary>Creates the gateway for the given page.</summary>
    /// <param name="page">The Playwright page to drive.</param>
    public HomePageGateway(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// Navigates to the home page, uploads a temporary file with the given name and content through the
    /// import widget, and waits for the import to finish. If the upload triggers the mass-import
    /// disambiguation dialog (shown when Playwright doesn't get the "import-success" indicator within 10s),
    /// this confirms the first available action in that dialog and then waits again for success, up to 30s.
    /// The temporary file is always deleted afterwards, even if the import fails.
    /// </summary>
    /// <param name="fileName">File name to present to the upload input; only the name matters, not a real path.</param>
    /// <param name="content">Text content written to the temporary file before it is uploaded.</param>
    public async Task UploadStatementFileAsync(string fileName, string content)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-{fileName}");
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            await _page.GotoAsync("/");
            await _page.Locator("#Import").WaitForAsync();
            await _page.Locator("#Import input[type=file]").SetInputFilesAsync(tempFile);

            var success = _page.Locator(".import-success");
            var dialog = _page.Locator(".mass-import-dialog");

            try
            {
                await success.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
                return;
            }
            catch (TimeoutException)
            {
                if (await dialog.CountAsync() == 0)
                {
                    throw;
                }
            }

            await dialog.Locator("button.btn").First.ClickAsync();
            await success.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
