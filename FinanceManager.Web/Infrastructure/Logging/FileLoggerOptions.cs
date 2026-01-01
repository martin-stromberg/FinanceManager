namespace FinanceManager.Web.Infrastructure.Logging;

/// <summary>
/// Options used to configure the file logger provider.
/// </summary>
public sealed class FileLoggerOptions
{
    /// <summary>
    /// File path format used for log files. The token "{date}" is replaced with the current date (format yyyyMMdd).
    /// Example: "logs/app-{date}.log".
    /// </summary>
    public string PathFormat { get; set; } = "logs/app-{date}.log"; // {date} → yyyyMMdd

    /// <summary>
    /// The maximum number of log files that are retained. Older files beyond this count may be removed.
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 30;

    /// <summary>
    /// The maximum allowed size in bytes for a single log file before a new file is created (roll on size).
    /// Default is 10 MB.
    /// </summary>
    public long FileSizeLimitBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// When <c>true</c> the logger will roll to a new file when the current file reaches <see cref="FileSizeLimitBytes"/>.
    /// </summary>
    public bool RollOnFileSizeLimit { get; set; } = true;

    /// <summary>
    /// When <c>true</c> log messages are appended to existing files; when <c>false</c> files are overwritten.
    /// </summary>
    public bool Append { get; set; } = true;

    /// <summary>
    /// When <c>true</c> include scope information from the logging scope in output if available.
    /// </summary>
    public bool IncludeScopes { get; set; } = false;

    /// <summary>
    /// When <c>true</c> timestamps in the log entries use UTC; otherwise the local machine time is used.
    /// </summary>
    public bool UseUtcTimestamp { get; set; } = false;
}