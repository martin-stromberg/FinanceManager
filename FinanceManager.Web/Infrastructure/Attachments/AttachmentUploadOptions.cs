namespace FinanceManager.Web.Infrastructure.Attachments;

/// <summary>
/// Options that control attachment upload limitations and allowed mime types.
/// </summary>
public sealed class AttachmentUploadOptions
{
    /// <summary>
    /// Maximum allowed upload size in bytes. Default is 10 MB.
    /// </summary>
    public long MaxSizeBytes { get; set; } = 10L * 1024L * 1024L;

    /// <summary>
    /// Whitelist of allowed MIME types for uploaded files. Requests with a content type not in this list should be rejected.
    /// </summary>
    /// <remarks>
    /// The default list contains common document and image formats as well as archive types.
    /// Customize in configuration when additional types are required.
    /// </remarks>
    public string[] AllowedMimeTypes { get; set; } = new[]
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "image/svg+xml",
        // support Windows icon formats
        "image/x-icon",
        "image/vnd.microsoft.icon",
        "text/plain",
        "text/csv",
        "application/zip"
    };
}
