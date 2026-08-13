using Microsoft.JSInterop;

namespace msTools.Web.Blazor;

/// <summary>
/// Provides a Blazor-side bridge for the reusable global loading bar.
/// </summary>
public sealed class LoadingBarService
{
    private readonly IJSRuntime _js;

    /// <summary>
    /// Creates a new loading bar bridge.
    /// </summary>
    /// <param name="js">JavaScript runtime used to call the browser-side loading bar API.</param>
    public LoadingBarService(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// Starts or restarts the global loading bar.
    /// </summary>
    public async Task StartAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("financeManager.loadingBar.start");
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>
    /// Stops the global loading bar.
    /// </summary>
    public async Task StopAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("financeManager.loadingBar.stop");
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>
    /// Runs an asynchronous UI action while the global loading bar is visible.
    /// </summary>
    /// <param name="action">Action to execute.</param>
    public async Task RunAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await StartAsync();
        try
        {
            await action();
        }
        finally
        {
            await StopAsync();
        }
    }

    /// <summary>
    /// Runs an asynchronous UI action while the global loading bar is visible and returns its result.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="action">Action to execute.</param>
    /// <returns>The action result.</returns>
    public async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await StartAsync();
        try
        {
            return await action();
        }
        finally
        {
            await StopAsync();
        }
    }
}
