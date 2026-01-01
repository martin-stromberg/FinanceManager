using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace FinanceManager.Web.Infrastructure.Logging;

/// <summary>
/// Logger provider that writes log entries to files on disk according to <see cref="FileLoggerOptions"/>.
/// Supports daily file naming, optional rolling by file size and retention of old log files.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly IOptionsMonitor<FileLoggerOptions> _optionsMonitor;
    private readonly IDisposable _onChange;
    private readonly object _writerLock = new();
    private FileLoggerOptions _options;
    private IExternalScopeProvider? _scopeProvider;
    private StreamWriter? _writer;
    private string _currentFilePath = string.Empty;
    private string _currentDateStamp = string.Empty;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLoggerProvider"/> class.
    /// </summary>
    /// <param name="optionsMonitor">Options monitor used to observe changes to <see cref="FileLoggerOptions"/>.</param>
    public FileLoggerProvider(IOptionsMonitor<FileLoggerOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
        _options = _optionsMonitor.CurrentValue;
        _onChange = _optionsMonitor.OnChange(o => { lock (_writerLock) { _options = o; RotateIfNeeded(force: true); } });
        RotateIfNeeded(force: true);
    }

    /// <summary>
    /// Creates or returns an existing logger for the specified category name.
    /// </summary>
    /// <param name="categoryName">Category name for the logger.</param>
    /// <returns>An <see cref="ILogger"/> instance that writes to the configured file target.</returns>
    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new FileLogger(this, name));

    /// <summary>
    /// Disposes the provider and flushes any outstanding log data to disk.
    /// </summary>
    public void Dispose()
    {
        _onChange.Dispose();
        lock (_writerLock)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }

    /// <summary>
    /// Sets the external scope provider used to capture logging scopes in output.
    /// </summary>
    /// <param name="scopeProvider">Scope provider instance.</param>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    /// <summary>
    /// Writes a log entry to the current log file, rotating files if necessary.
    /// This method is called by <see cref="FileLogger"/> instances and is thread-safe.
    /// </summary>
    /// <param name="category">Logging category for the entry.</param>
    /// <param name="level">Logging level.</param>
    /// <param name="eventId">Event id associated with the entry.</param>
    /// <param name="message">Formatted log message text.</param>
    /// <param name="exception">Optional exception associated with the entry.</param>
    /// <param name="includeScopes">When true, the current logging scopes are appended to the message when available.</param>
    internal void Log(string category, LogLevel level, EventId eventId, string message, Exception? exception, bool includeScopes)
    {
        var now = _options.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
        var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);

        var sb = new StringBuilder(512);
        sb.Append(timestamp)
          .Append(" [").Append(level.ToString().ToUpperInvariant()).Append(']')
          .Append(' ').Append(category);

        if (eventId.Id != 0)
        {
            sb.Append(" (").Append(eventId.Id.ToString(CultureInfo.InvariantCulture)).Append(')');
        }

        if (includeScopes && _scopeProvider is not null)
        {
            _scopeProvider.ForEachScope((scope, state) =>
            {
                state.Append(" => ").Append(scope);
            }, sb);
        }

        if (!string.IsNullOrEmpty(message))
        {
            sb.Append(' ').Append(message);
        }

        if (exception is not null)
        {
            sb.AppendLine().Append(exception);
        }

        var line = sb.ToString();

        lock (_writerLock)
        {
            RotateIfNeeded(force: false);
            _writer!.WriteLine(line);
            _writer.Flush();
            if (_options.RollOnFileSizeLimit && _writer.BaseStream is FileStream fs && fs.Length >= _options.FileSizeLimitBytes)
            {
                RotateIfNeeded(force: true);
            }
        }
    }

    /// <summary>
    /// Ensures the current writer targets the correct file for the current date and roll/size settings.
    /// </summary>
    /// <param name="force">When <c>true</c> forces rotation even if the target path matches the current path.</param>
    private void RotateIfNeeded(bool force)
    {
        var dateStamp = (_options.UseUtcTimestamp ? DateTime.UtcNow : DateTime.Now).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var targetPath = BuildFilePath(dateStamp);

        if (!force && string.Equals(_currentFilePath, targetPath, StringComparison.Ordinal))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        // Close existing writer
        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;

        // If rolling by size, pick next available index
        var finalPath = targetPath;
        if (_options.RollOnFileSizeLimit && File.Exists(finalPath) && new FileInfo(finalPath).Length >= _options.FileSizeLimitBytes)
        {
            finalPath = NextSizedPath(targetPath);
        }

        var stream = new FileStream(finalPath,
            _options.Append ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.Read);

        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
        _currentFilePath = finalPath;
        _currentDateStamp = dateStamp;

        EnforceRetention();
    }

    /// <summary>
    /// Builds the full file path for the configured path format by replacing the date token and resolving relative paths.
    /// </summary>
    /// <param name="dateStamp">Date stamp string (yyyyMMdd) to substitute into the path format.</param>
    /// <returns>Absolute file path used for logging.</returns>
    private string BuildFilePath(string dateStamp)
    {
        var baseDir = AppContext.BaseDirectory;
        var raw = _options.PathFormat.Replace("{date}", dateStamp, StringComparison.OrdinalIgnoreCase);
        var full = Path.IsPathRooted(raw) ? raw : Path.GetFullPath(raw, baseDir);
        return full;
    }

    /// <summary>
    /// Computes the next available file path when rolling by size. The new file name uses a numeric suffix (e.g. _001).
    /// </summary>
    /// <param name="basePath">Base file path to derive the next sized path for.</param>
    /// <returns>Path for the next rolled file.</returns>
    private static string NextSizedPath(string basePath)
    {
        var dir = Path.GetDirectoryName(basePath)!;
        var filename = Path.GetFileNameWithoutExtension(basePath);
        var ext = Path.GetExtension(basePath);

        int index = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{filename}_{index:D3}{ext}");
            index++;
        } while (File.Exists(candidate) && new FileInfo(candidate).Length > 0);

        return candidate;
    }

    /// <summary>
    /// Enforces the configured retention policy by deleting older log files beyond the retained count.
    /// Failures are intentionally ignored to avoid impacting logging.
    /// </summary>
    private void EnforceRetention()
    {
        try
        {
            var dir = Path.GetDirectoryName(_currentFilePath)!;
            var name = Path.GetFileNameWithoutExtension(_currentFilePath);
            var ext = Path.GetExtension(_currentFilePath);

            // Match all files for the same base (e.g., app-YYYYMMDD*)
            var basePrefix = name.Split('_')[0]; // strip size suffix
            var pattern = $"{basePrefix}*{ext}";
            var files = new DirectoryInfo(dir).GetFiles(pattern)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            for (int i = _options.RetainedFileCountLimit; i < files.Count; i++)
            {
                try { files[i].Delete(); } catch { /* ignore */ }
            }
        }
        catch
        {
            // ignore retention failures
        }
    }
}

/// <summary>
/// Logger implementation that delegates formatted log entries to its owning <see cref="FileLoggerProvider"/>.
/// </summary>
internal sealed class FileLogger : ILogger
{
    private readonly FileLoggerProvider _provider;
    private readonly string _category;

    /// <summary>
    /// Initializes a new instance of <see cref="FileLogger"/> for the specified category.
    /// </summary>
    /// <param name="provider">Owning file logger provider.</param>
    /// <param name="category">Logging category name.</param>
    public FileLogger(FileLoggerProvider provider, string category)
    {
        _provider = provider;
        _category = category;
    }

    /// <summary>
    /// Begins a logical operation scope. This implementation does not support scopes and returns <c>null</c>.
    /// </summary>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default!;

    /// <summary>
    /// Indicates whether logging is enabled for the specified log level.
    /// </summary>
    /// <param name="logLevel">Log level to test.</param>
    /// <returns><c>true</c> when the provider accepts the level; otherwise <c>false</c>.</returns>
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <summary>
    /// Formats and emits a log entry via the owning provider.
    /// </summary>
    /// <typeparam name="TState">State type provided by the logger caller.</typeparam>
    /// <param name="logLevel">The log level for the entry.</param>
    /// <param name="eventId">Event id associated with the entry.</param>
    /// <param name="state">State object provided by the caller.</param>
    /// <param name="exception">Optional exception associated with the entry.</param>
    /// <param name="formatter">Function that formats the state and exception into a message string.</param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        _provider.Log(_category, logLevel, eventId, message, exception, includeScopes: false);
    }
}