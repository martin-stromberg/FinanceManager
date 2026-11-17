using FinanceManager.Application.Mcp;
using FinanceManager.Infrastructure.Mcp;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of the MCP token issuer (format <c>fm_mcp_&lt;hint&gt;_&lt;secret&gt;</c>,
/// ≥160-bit secret) and the PBKDF2 token hasher (<c>pbkdf2|iters|salt|key</c>).
/// </summary>
public sealed class McpTokenIssuerAndHasherTests
{
    private static McpTokenHasher NewHasher(int iterations = 1000)
    {
        var opts = new TestOptionsMonitor<McpOptions>(new McpOptions
        {
            TokenHash = new McpTokenHashOptions { Iterations = iterations }
        });
        return new McpTokenHasher(opts);
    }

    private static McpTokenIssuer NewIssuer() => new(NewHasher());

    /// <summary>Test case.</summary>
    [Fact]
    public void Issue_ProducesExpectedFormat_AndVerifiableHash()
    {
        var hasher = NewHasher();
        var issuer = new McpTokenIssuer(hasher);
        var issued = issuer.Issue();

        Assert.StartsWith("fm_mcp_", issued.PlainText);
        Assert.True(issuer.TryParse(issued.PlainText, out var hint, out var secret));
        Assert.Equal(issued.AccessKeyHint, hint);
        Assert.True(hint.Length <= 12 && hint.Length > 0);
        // Base64url secret of 32 bytes: 43 chars, >= 160 bit entropy.
        Assert.True(secret.Length >= 40);
        Assert.True(hasher.Verify(secret, issued.TokenHash));
        Assert.False(hasher.Verify(secret + "x", issued.TokenHash));
    }

    /// <summary>Test case.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("fm_mcp_")]
    [InlineData("bearer fm_mcp_ABC_secret")]
    [InlineData("fm_mcp_hint")]               // missing secret
    [InlineData("fm_mcp__secretsecretsecretsecretsecret")] // empty hint
    [InlineData("fm_mcp_hint_short")]        // secret below minimum
    [InlineData("fm_mcp_hinttoolonggggg_secretsecretsecretsecret")] // hint > 12 chars
    public void TryParse_RejectsMalformedTokens(string token)
    {
        var issuer = NewIssuer();
        Assert.False(issuer.TryParse(token, out _, out _));
    }

    /// <summary>Test case.</summary>
    [Fact]
    public void Hash_IsSalted_AndSelfVerifying()
    {
        var hasher = NewHasher();
        var h1 = hasher.Hash("secret-a");
        var h2 = hasher.Hash("secret-a");
        Assert.NotEqual(h1, h2); // different salts
        Assert.StartsWith("pbkdf2|1000|", h1);
        Assert.True(hasher.Verify("secret-a", h1));
        Assert.True(hasher.Verify("secret-a", h2));
        Assert.False(hasher.Verify("secret-b", h1));
    }

    /// <summary>Test case.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("pbkdf2|abc|AA|AA")]
    [InlineData("pbkdf2|0|AA|AA")]
    [InlineData("pbkdf2|1000|!!!|AA")]
    [InlineData("pbkdf2|1000|AA")]
    [InlineData("other|1000|AA|AA")]
    public void Verify_RejectsMalformedHashes(string stored)
    {
        Assert.False(NewHasher().Verify("secret", stored));
    }

    /// <summary>Minimal IOptionsMonitor stub for unit tests.</summary>
    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
    {
        public TestOptionsMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
