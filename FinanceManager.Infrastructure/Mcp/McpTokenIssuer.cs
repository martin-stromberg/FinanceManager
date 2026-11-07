using FinanceManager.Application.Mcp;
using System.Security.Cryptography;
using System.Text;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Issues and parses MCP personal access tokens of the form
/// <c>fm_mcp_&lt;hint&gt;_&lt;secret&gt;</c> (sicherheitskonzept.md §3.1). The hint is a
/// random, non-secret lookup key (8 Base32 characters); the secret carries 256 bits of
/// entropy (Base64url, 43 characters) — well above the required 160 bits.
/// </summary>
public sealed class McpTokenIssuer : IMcpTokenIssuer
{
    /// <summary>Fixed token prefix.</summary>
    public const string Prefix = "fm_mcp_";

    /// <summary>Length of the access key hint in characters (fits the 12-char column).</summary>
    public const int HintLength = 8;

    private const int SecretBytes = 32; // 256 bit >= 160 bit required entropy
    private const string Base32Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I/O/0/1 ambiguity

    private readonly IMcpTokenHasher _hasher;

    /// <summary>
    /// Creates a new instance of <see cref="McpTokenIssuer"/>.
    /// </summary>
    /// <param name="hasher">The token hasher used to hash the issued secret.</param>
    public McpTokenIssuer(IMcpTokenHasher hasher)
    {
        _hasher = hasher;
    }

    /// <inheritdoc />
    public McpIssuedToken Issue()
    {
        var hint = GenerateHint();
        var secret = Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
        var plainText = $"{Prefix}{hint}_{secret}";
        return new McpIssuedToken(plainText, hint, _hasher.Hash(secret));
    }

    /// <inheritdoc />
    public bool TryParse(string token, out string accessKeyHint, out string secret)
    {
        accessKeyHint = string.Empty;
        secret = string.Empty;
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var remainder = token[Prefix.Length..];
        var separator = remainder.IndexOf('_');
        if (separator <= 0 || separator == remainder.Length - 1)
        {
            return false;
        }

        var hint = remainder[..separator];
        var sec = remainder[(separator + 1)..];
        // The secret is Base64url and may legitimately contain '_' — the first underscore
        // after the prefix is the hint/secret separator, everything beyond is the secret.
        if (hint.Length == 0 || hint.Length > 12 || hint.Contains('_') || sec.Length < 20)
        {
            return false;
        }

        accessKeyHint = hint;
        secret = sec;
        return true;
    }

    private static string GenerateHint()
    {
        var bytes = RandomNumberGenerator.GetBytes(HintLength);
        var chars = new char[HintLength];
        for (var i = 0; i < HintLength; i++)
        {
            chars[i] = Base32Alphabet[bytes[i] % Base32Alphabet.Length];
        }
        return new string(chars);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
