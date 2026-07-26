using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;

namespace FinanceManager.Tests.E2E;

public sealed class PlaywrightWebAppFixture : IAsyncLifetime
{
    public sealed class PlaywrightSessionOptions
    {
        public ViewportSize? ViewportSize { get; init; }
        public bool? IsMobile { get; init; }
        public bool? HasTouch { get; init; }
        public string? Locale { get; init; }
    }

    private static readonly PlaywrightSessionOptions MobileSessionOptions = new()
    {
        ViewportSize = new ViewportSize { Width = 390, Height = 844 },
        IsMobile = true,
        HasTouch = true
    };

    private readonly PlaywrightTestOptions _options = new();
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private Process? _server;
    private string? _baseUrl;
    private string? _dbPath;
    private readonly StringBuilder _serverOutput = new();
    private readonly StringBuilder _serverError = new();

    public string BaseUrl => _baseUrl ?? throw new InvalidOperationException("The Playwright server is not initialized.");
    public string DatabasePath => _dbPath ?? throw new InvalidOperationException("The Playwright database is not initialized.");

    public async ValueTask InitializeAsync()
    {
        var port = GetFreePort();
        _baseUrl = $"https://127.0.0.1:{port}";
        _dbPath = Path.Combine(Path.GetTempPath(), $"financemanager-e2e-{Guid.NewGuid():N}.db");

        var webDll = ResolveWebDllPath();
        StartServer(port, webDll, _dbPath);
        await WaitForServerAsync();

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await LaunchBrowserAsync(_playwright);
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            try
            {
                await _browser.CloseAsync();
            }
            catch
            {
                // Best-effort cleanup only.
            }

            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;

        if (_server != null)
        {
            try
            {
                if (!_server.HasExited)
                {
                    _server.Kill(entireProcessTree: true);
                    await _server.WaitForExitAsync();
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }

            _server.Dispose();
            _server = null;
        }

        if (!string.IsNullOrWhiteSpace(_dbPath) && File.Exists(_dbPath))
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    File.Delete(_dbPath);
                    break;
                }
                catch (IOException) when (attempt < 4)
                {
                    await Task.Delay(200);
                }
                catch
                {
                    // Best-effort cleanup only.
                    break;
                }
            }
        }
    }

    public async Task<PlaywrightBrowserSession> CreateSessionAsync(PlaywrightSessionOptions? options = null)
    {
        if (_browser == null)
        {
            throw new InvalidOperationException("Browser is not initialized.");
        }

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            IgnoreHTTPSErrors = true,
            ViewportSize = options?.ViewportSize,
            IsMobile = options?.IsMobile,
            HasTouch = options?.HasTouch,
            Locale = options?.Locale,
        });
        context.SetDefaultTimeout(_options.ActionTimeoutSeconds * 1000);
        context.SetDefaultNavigationTimeout(_options.NavigationTimeoutSeconds * 1000);

        var artifactPrefix = (_options.ArtifactCaptureEnabled || _options.TraceEnabled)
            ? Path.Combine(GetArtifactDirectory(), $"session-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}")
            : null;
        if (_options.TraceEnabled)
        {
            await context.Tracing.StartAsync(new()
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true
            });
        }

        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(_options.ActionTimeoutSeconds * 1000);
        page.SetDefaultNavigationTimeout(_options.NavigationTimeoutSeconds * 1000);
        return new PlaywrightBrowserSession(context, page, artifactPrefix, _options.ArtifactCaptureEnabled, _options.TraceEnabled);
    }

    public Task<PlaywrightBrowserSession> CreateMobileSessionAsync()
        => CreateSessionAsync(MobileSessionOptions);

    private async Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright)
    {
        var opts = new BrowserTypeLaunchOptions
        {
            Headless = _options.Headless,
        };

        if (!string.IsNullOrWhiteSpace(_options.BrowserChannel))
        {
            opts.Channel = _options.BrowserChannel;
        }

        try
        {
            return await playwright.Chromium.LaunchAsync(opts);
        }
        catch
        {
            if (!string.Equals(_options.BrowserChannel, "chromium", StringComparison.OrdinalIgnoreCase))
            {
                opts.Channel = null;
                return await playwright.Chromium.LaunchAsync(opts);
            }

            throw;
        }
    }

    private void StartServer(int port, string webDll, string dbPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{webDll}\"",
            WorkingDirectory = Path.Combine(GetRepoRoot(), "FinanceManager.Web"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_URLS"] = $"https://127.0.0.1:{port};http://127.0.0.1:{port + 1}";
        startInfo.Environment["Kestrel__Endpoints__Http__Url"] = $"http://127.0.0.1:{port + 1}";
        startInfo.Environment["Kestrel__Endpoints__Https__Url"] = $"https://127.0.0.1:{port}";
        startInfo.Environment["Api__BaseAddress"] = $"http://127.0.0.1:{port + 1}/";
        startInfo.Environment["E2E__DisableHttpsRedirection"] = "true";
        startInfo.Environment["ConnectionStrings__Default"] = $"Data Source={dbPath}";
        startInfo.Environment["BackgroundTasks__Enabled"] = "false";
        startInfo.Environment["Workers__SecurityPriceWorker__Enabled"] = "false";
        startInfo.Environment["FileLogging__Enabled"] = "false";
        startInfo.Environment["DetailedErrors"] = "true";

        _server = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the Playwright test server.");
        _server.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                lock (_serverOutput)
                {
                    _serverOutput.AppendLine(e.Data);
                }
            }
        };
        _server.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                lock (_serverError)
                {
                    _serverError.AppendLine(e.Data);
                }
            }
        };
        _server.BeginOutputReadLine();
        _server.BeginErrorReadLine();
    }

    private async Task WaitForServerAsync()
    {
        using var client = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_server is { HasExited: true })
            {
                throw new InvalidOperationException($"The Playwright test server exited early: {GetServerLogs()}");
            }

            try
            {
                using var response = await client.GetAsync($"{BaseUrl}/login");
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.RedirectKeepVerb)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"The Playwright test server did not become ready at {BaseUrl}. Logs: {GetServerLogs()}", lastError);
    }

    private string GetServerLogs()
    {
        lock (_serverOutput)
        lock (_serverError)
        {
            return string.Join(Environment.NewLine, new[]
            {
                _serverOutput.ToString(),
                _serverError.ToString()
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }

    private static string GetArtifactDirectory()
    {
        var path = Path.Combine(GetRepoRoot(), "TestResults", "E2E", "artifacts");
        Directory.CreateDirectory(path);
        return path;
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 4; i++)
        {
            dir = dir.Parent ?? throw new InvalidOperationException("Unable to resolve repository root.");
        }

        return dir.FullName;
    }

    private static string ResolveWebDllPath()
    {
        var root = GetRepoRoot();
        var candidates = new[]
        {
            Path.Combine(root, "FinanceManager.Web", "bin", "Debug", "net10.0", "FinanceManager.Web.dll"),
            Path.Combine(root, "FinanceManager.Web", "bin", "Release", "net10.0", "FinanceManager.Web.dll"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Could not locate the built FinanceManager.Web.dll.");
    }
}
