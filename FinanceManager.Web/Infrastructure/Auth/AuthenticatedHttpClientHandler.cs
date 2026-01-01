using System.Net;
using System.Net.Http.Headers;

/// <summary>
/// HTTP message handler that attaches a bearer access token obtained from an <see cref="IAuthTokenProvider"/> to outgoing requests.
/// The handler deliberately requests the token using <see cref="CancellationToken.None"/> so that token retrieval is not cancelled
/// when the originating HTTP request is cancelled (avoids spurious cancellations during UI navigation).
/// </summary>
public sealed class AuthenticatedHttpClientHandler : DelegatingHandler
{
    private readonly IAuthTokenProvider _tokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticatedHttpClientHandler"/> class.
    /// </summary>
    /// <param name="tokenProvider">Provider responsible for returning a bearer access token. Must not be <c>null</c>.</param>
    public AuthenticatedHttpClientHandler(IAuthTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    /// <summary>
    /// Sends an HTTP request with an Authorization header attached when an access token can be retrieved.
    /// Token retrieval is performed without honoring the provided <paramref name="cancellationToken"/>
    /// to avoid cancelling token acquisition during navigation; token acquisition failures are silently ignored
    /// and the request proceeds without an Authorization header.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="cancellationToken">Cancellation token that may be used to cancel the request.</param>
    /// <returns>
    /// A task that represents the asynchronous send operation. The task result contains the HTTP response message.
    /// </returns>
    /// <remarks>
    /// If the outgoing request is cancelled by the caller (i.e. the provided <paramref name="cancellationToken"/> is signalled)
    /// an <see cref="OperationCanceledException"/> thrown by the inner handler is intercepted and converted into a
    /// synthetic HTTP response with status code 499 (Client Closed Request) to avoid breaking the debugger during development.
    /// Other exceptions from the inner handler (for example <see cref="HttpRequestException"/>) are propagated to the caller.
    /// </remarks>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Do not tie token retrieval to request cancellation to avoid spurious cancellations during navigation
        string? token = null;
        try
        {
            token = await _tokenProvider.GetAccessTokenAsync(CancellationToken.None);
        }
        catch
        {
            // ignore token retrieval errors; proceed without auth header
        }

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Convert client-triggered cancellations to a synthetic HTTP response to avoid breaking the debugger
            var resp = new HttpResponseMessage((HttpStatusCode)499)
            {
                RequestMessage = request,
                ReasonPhrase = "Client Closed Request"
            };
            return resp;
        }
    }
}