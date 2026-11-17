using System.Net;
using System.Net.Sockets;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Evaluates the optional per-access IP binding (<see cref="FinanceManager.Domain.Mcp.McpAccess.AllowedIpRanges"/>):
/// a comma-separated list of single IP addresses or CIDR ranges.
/// </summary>
public static class McpIpRangeMatcher
{
    /// <summary>
    /// Determines whether <paramref name="remoteIp"/> is allowed by the binding.
    /// </summary>
    /// <param name="allowedIpRanges">CSV of addresses/CIDR ranges; null/empty = unrestricted.</param>
    /// <param name="remoteIp">The client IP.</param>
    /// <returns><see langword="true"/> when unrestricted or the IP matches any entry.</returns>
    public static bool IsAllowed(string? allowedIpRanges, IPAddress? remoteIp)
    {
        if (string.IsNullOrWhiteSpace(allowedIpRanges))
        {
            return true;
        }
        if (remoteIp is null)
        {
            return false;
        }
        // Normalize IPv4-mapped IPv6 so "::ffff:192.168.1.5" matches "192.168.1.5" entries.
        var ip = remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4() : remoteIp;

        foreach (var entry in allowedIpRanges.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (MatchesEntry(entry, ip))
            {
                return true;
            }
        }
        return false;
    }

    private static bool MatchesEntry(string entry, IPAddress ip)
    {
        var slash = entry.IndexOf('/');
        if (slash < 0)
        {
            return IPAddress.TryParse(entry, out var single)
                && Normalize(single).Equals(ip);
        }
        if (!IPAddress.TryParse(entry[..slash], out var network) || !int.TryParse(entry[(slash + 1)..], out var prefix))
        {
            return false;
        }
        network = Normalize(network);
        if (network.AddressFamily != ip.AddressFamily)
        {
            return false;
        }
        var maxBits = network.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (prefix < 0 || prefix > maxBits)
        {
            return false;
        }
        var netBytes = network.GetAddressBytes();
        var ipBytes = ip.GetAddressBytes();
        var fullBytes = prefix / 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (netBytes[i] != ipBytes[i])
            {
                return false;
            }
        }
        var remainingBits = prefix % 8;
        if (remainingBits == 0)
        {
            return true;
        }
        var mask = (byte)(0xFF << (8 - remainingBits));
        return (netBytes[fullBytes] & mask) == (ipBytes[fullBytes] & mask);
    }

    private static IPAddress Normalize(IPAddress address)
        => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
