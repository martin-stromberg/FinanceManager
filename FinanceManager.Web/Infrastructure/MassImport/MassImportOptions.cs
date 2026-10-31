namespace FinanceManager.Web.Infrastructure.MassImport;

/// <summary>
/// Options that bound the mass-import batch endpoint: the total JSON request body size,
/// the number of files per batch, the raw byte size of a single file, and the summed raw
/// content bytes of all files.
/// </summary>
public sealed class MassImportOptions
{
    /// <summary>Configuration section path for mass-import limits.</summary>
    public const string SectionName = "MassImport";

    /// <summary>
    /// Maximum accepted JSON request body size in bytes. Must cover the base64 inflation
    /// of <see cref="MaxTotalContentBytes"/> plus the remaining request payload.
    /// </summary>
    public long MaxRequestBytes { get; set; } = 150_000_000;

    /// <summary>Maximum number of files accepted in a single batch.</summary>
    public int MaxFiles { get; set; } = 50;

    /// <summary>Maximum raw content bytes accepted per file.</summary>
    public long MaxBytesPerFile { get; set; } = 10L * 1024L * 1024L;

    /// <summary>Maximum summed raw content bytes across all files in a batch.</summary>
    public long MaxTotalContentBytes { get; set; } = 100L * 1024L * 1024L;
}
