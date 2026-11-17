using FinanceManager.Web.Mcp;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of the per-access IP-binding matcher (single addresses, CIDR, IPv4-mapped IPv6).
/// </summary>
public sealed class McpIpRangeMatcherTests
{
    /// <summary>Test case.</summary>
    [Fact]
    public void NullOrEmptyBinding_AllowsEverything()
    {
        Assert.True(McpIpRangeMatcher.IsAllowed(null, IPAddress.Parse("1.2.3.4")));
        Assert.True(McpIpRangeMatcher.IsAllowed("  ", IPAddress.Parse("1.2.3.4")));
        Assert.True(McpIpRangeMatcher.IsAllowed(null, null));
    }

    /// <summary>Test case.</summary>
    [Fact]
    public void BindingWithoutRemoteIp_Denies()
        => Assert.False(McpIpRangeMatcher.IsAllowed("10.0.0.1", null));

    /// <summary>Test case.</summary>
    [Theory]
    [InlineData("10.0.0.1", "10.0.0.1", true)]
    [InlineData("10.0.0.1", "10.0.0.2", false)]
    [InlineData("192.168.0.0/24", "192.168.0.55", true)]
    [InlineData("192.168.0.0/24", "192.168.1.5", false)]
    [InlineData("10.0.0.0/8,192.168.1.1", "192.168.1.1", true)]
    [InlineData("10.0.0.0/8,192.168.1.1", "172.16.0.1", false)]
    [InlineData("0.0.0.0/0", "203.0.113.7", true)]
    [InlineData("2001:db8::/32", "2001:db8::1", true)]
    [InlineData("2001:db8::/32", "2001:db9::1", false)]
    [InlineData("not-an-ip", "10.0.0.1", false)]
    [InlineData("10.0.0.0/33", "10.0.0.1", false)]
    public void Entries_MatchAsExpected(string ranges, string ip, bool expected)
        => Assert.Equal(expected, McpIpRangeMatcher.IsAllowed(ranges, IPAddress.Parse(ip)));

    /// <summary>Test case.</summary>
    [Fact]
    public void Ipv4MappedIpv6_MatchesIpv4Entry()
    {
        var mapped = IPAddress.Parse("::ffff:192.168.0.5");
        Assert.True(McpIpRangeMatcher.IsAllowed("192.168.0.0/24", mapped));
        Assert.True(McpIpRangeMatcher.IsAllowed("192.168.0.5", mapped));
    }
}
