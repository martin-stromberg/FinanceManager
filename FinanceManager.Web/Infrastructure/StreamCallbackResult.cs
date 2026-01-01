using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace FinanceManager.Web.Infrastructure;

/// <summary>
/// Streams a response body using a provided async callback, setting Content-Type and Content-Disposition.
/// This helper avoids direct Response manipulation inside controllers while enabling large streaming writes
/// through a user-supplied callback that writes to the provided response stream.
/// </summary>
public sealed class StreamCallbackResult : IActionResult
{
    private readonly string _contentType;
    private readonly Func<Stream, CancellationToken, Task> _callback;

    /// <summary>
    /// Optional file name that will be used to set the <c>Content-Disposition</c> header as an attachment.
    /// When set the header will contain an ASCII fallback filename and a UTF-8 encoded <c>filename*</c> per RFC 6266.
    /// </summary>
    public string? FileDownloadName { get; init; }

    /// <summary>
    /// Initializes a new instance of <see cref="StreamCallbackResult"/>.
    /// </summary>
    /// <param name="contentType">MIME content type to set on the response, e.g. "application/octet-stream".</param>
    /// <param name="callback">Async callback that will be invoked with the response stream and a cancellation token.
    /// The callback is expected to write the response body to the provided stream.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentType"/> or <paramref name="callback"/> is <c>null</c>.</exception>
    public StreamCallbackResult(string contentType, Func<Stream, CancellationToken, Task> callback)
    {
        _contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    /// <summary>
    /// Executes the result by setting response headers and invoking the configured write callback.
    /// </summary>
    /// <param name="context">The action context containing the <see cref="HttpContext"/>. Must not be <c>null</c>.</param>
    /// <returns>A task that completes when the callback has finished writing to the response stream.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Any exceptions thrown by the user-provided callback will propagate to the ASP.NET Core pipeline.
    /// The callback should observe the provided cancellation token and stop writing when it is signalled.
    /// </remarks>
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.ContentType = _contentType;

        if (!string.IsNullOrWhiteSpace(FileDownloadName))
        {
            // RFC 6266: provide ASCII fallback filename plus UTF-8 encoded filename*
            var ascii = ToAscii(FileDownloadName!);
            var utf8Star = Uri.EscapeDataString(FileDownloadName!);
            var cd = $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{utf8Star}";
            response.Headers[HeaderNames.ContentDisposition] = cd;
        }

        await _callback(response.Body, context.HttpContext.RequestAborted);
    }

    /// <summary>
    /// Produces an ASCII-only fallback for a filename by replacing non-ASCII characters with underscores.
    /// </summary>
    /// <param name="name">Input filename to convert.</param>
    /// <returns>ASCII-only string safe for use in the <c>filename</c> token of Content-Disposition header.</returns>
    private static string ToAscii(string name)
    {
        if (string.IsNullOrEmpty(name)) { return string.Empty; }
        Span<char> buffer = stackalloc char[name.Length];
        var i = 0;
        foreach (var ch in name)
        {
            buffer[i++] = ch <= 0x7F ? ch : '_';
        }
        return new string(buffer[..i]);
    }
}
