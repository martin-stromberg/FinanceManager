namespace FinanceManager.Application.Mcp;

/// <summary>
/// One-way hashing of MCP token secrets following the
/// <c>Pbkdf2IdentityPasswordHasher</c> storage format
/// (<c>pbkdf2|&lt;iterations&gt;|&lt;salt-b64&gt;|&lt;key-b64&gt;</c>,
/// <c>FixedTimeEquals</c> verification). The iteration count comes from
/// <see cref="McpTokenHashOptions.Iterations"/> (sicherheitskonzept.md §3.2).
/// </summary>
public interface IMcpTokenHasher
{
    /// <summary>
    /// Computes a salted PBKDF2 hash of the supplied secret.
    /// </summary>
    /// <param name="secret">The plaintext token secret. Must not be <c>null</c>.</param>
    /// <returns>The formatted hash string.</returns>
    string Hash(string secret);

    /// <summary>
    /// Verifies a supplied secret against a stored hash. Malformed hashes yield
    /// <see langword="false"/> instead of throwing (no detail leak).
    /// </summary>
    /// <param name="secret">The plaintext secret to verify.</param>
    /// <param name="storedHash">The stored hash produced by <see cref="Hash"/>.</param>
    /// <returns><see langword="true"/> when the secret matches.</returns>
    bool Verify(string secret, string storedHash);
}
