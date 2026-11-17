namespace FinanceManager.Application.Mcp;

/// <summary>
/// An issued MCP personal access token. <see cref="PlainText"/> is shown to the user exactly
/// once (creation or rotation) and is never persisted; only <see cref="TokenHash"/> is stored.
/// </summary>
/// <param name="PlainText">Full token in <c>fm_mcp_&lt;hint&gt;_&lt;secret&gt;</c> form.</param>
/// <param name="AccessKeyHint">Non-secret lookup key embedded in the token.</param>
/// <param name="TokenHash">One-way hash of the secret (<c>pbkdf2|…</c> format).</param>
public sealed record McpIssuedToken(string PlainText, string AccessKeyHint, string TokenHash);

/// <summary>
/// Issues MCP personal access tokens of the form <c>fm_mcp_&lt;zugangskürzel&gt;_&lt;secret&gt;</c>
/// and parses them back into their parts (sicherheitskonzept.md §3.1).
/// </summary>
public interface IMcpTokenIssuer
{
    /// <summary>
    /// Issues a new token: random access key hint plus a cryptographically random secret
    /// (≥160 bit entropy, Base64url-encoded). The returned <see cref="McpIssuedToken.TokenHash"/>
    /// is the only value that may be persisted.
    /// </summary>
    /// <returns>The issued token material.</returns>
    McpIssuedToken Issue();

    /// <summary>
    /// Splits a candidate token into access key hint and secret without verifying anything.
    /// </summary>
    /// <param name="token">The raw token (without the <c>Bearer</c> prefix).</param>
    /// <param name="accessKeyHint">The extracted hint when parsing succeeds.</param>
    /// <param name="secret">The extracted secret when parsing succeeds.</param>
    /// <returns><see langword="true"/> when the token matches the <c>fm_mcp_*</c> shape.</returns>
    bool TryParse(string token, out string accessKeyHint, out string secret);
}
