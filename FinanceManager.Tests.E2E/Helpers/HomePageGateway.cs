namespace FinanceManager.Tests.E2E;

public sealed class HomePageGateway
{
    private readonly IPage _page;

    public HomePageGateway(IPage page)
    {
        _page = page;
    }

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
