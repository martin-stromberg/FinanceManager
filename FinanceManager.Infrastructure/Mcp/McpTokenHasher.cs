using FinanceManager.Application.Mcp;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// PBKDF2-HMACSHA256 one-way hasher of MCP token secrets. Uses the same storage format as
/// <c>Pbkdf2IdentityPasswordHasher</c> (<c>pbkdf2|&lt;iterations&gt;|&lt;salt-b64&gt;|&lt;key-b64&gt;</c>)
/// with the iteration count configured via <c>Mcp:TokenHash:Iterations</c>
/// (sicherheitskonzept.md §3.2).
/// </summary>
public sealed class McpTokenHasher : IMcpTokenHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;

    private readonly IOptionsMonitor<McpOptions> _options;

    /// <summary>
    /// Creates a new instance of <see cref="McpTokenHasher"/>.
    /// </summary>
    /// <param name="options">The MCP options monitor (iteration count, reloadable).</param>
    public McpTokenHasher(IOptionsMonitor<McpOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public string Hash(string secret)
    {
        if (secret is null) throw new ArgumentNullException(nameof(secret));
        var iterations = Math.Max(1, _options.CurrentValue.TokenHash.Iterations);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = KeyDerivation.Pbkdf2(secret, salt, KeyDerivationPrf.HMACSHA256, iterations, KeySize);
        return $"pbkdf2|{iterations}|{Convert.ToBase64String(salt)}|{Convert.ToBase64String(key)}";
    }

    /// <inheritdoc />
    public bool Verify(string secret, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('|');
        if (parts.Length != 4 || parts[0] != "pbkdf2")
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var key = Convert.FromBase64String(parts[3]);
            var attempted = KeyDerivation.Pbkdf2(secret, salt, KeyDerivationPrf.HMACSHA256, iterations, key.Length);
            return CryptographicOperations.FixedTimeEquals(key, attempted);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
