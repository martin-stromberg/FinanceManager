using FinanceManager.Application.Mcp;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Text;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Stateless download-ticket service behind <c>GET /mcp/download/{token}</c>: the ticket
/// payload (<c>v1|owner|backup|access|expiresTicks</c>) is protected with ASP.NET Core
/// DataProtection (encrypted + tamper-proof, purpose-isolated) and carried Base64Url-encoded
/// in the URL — no database record, no migration and no cleanup job. The trade-off of the
/// stateless variant (no single-use consumption) is accepted for a read-only artifact
/// guarded by the short <see cref="DefaultLifetime"/>. Keys come from the shared
/// DataProtection key ring (<c>DataProtection:KeysPath</c>); a key rotation only invalidates
/// outstanding ≤10-minute tickets.
/// </summary>
public sealed class McpDownloadTicketService : IMcpDownloadTicketService
{
    /// <summary>Default ticket lifetime (10 minutes).</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(10);

    private const string Purpose = "FinanceManager.Mcp.DownloadTicket.v1";
    private const string Version = "v1";
    private const int MaxTokenLength = 1024;

    private readonly IDataProtector _protector;
    private readonly TimeProvider _clock;

    /// <summary>Creates the service.</summary>
    /// <param name="provider">Shared DataProtection provider (purpose-derived protector).</param>
    /// <param name="clock">Time source for issue/expiry comparison.</param>
    public McpDownloadTicketService(IDataProtectionProvider provider, TimeProvider clock)
    {
        _protector = provider.CreateProtector(Purpose);
        _clock = clock;
    }

    /// <inheritdoc />
    public McpDownloadTicket Issue(Guid ownerUserId, Guid backupId, Guid? mcpAccessId, TimeSpan? lifetime = null)
    {
        var expires = _clock.GetUtcNow().UtcDateTime.Add(lifetime ?? DefaultLifetime);
        var payload = string.Join('|',
            Version,
            ownerUserId.ToString("N"),
            backupId.ToString("N"),
            mcpAccessId?.ToString("N") ?? "-",
            expires.Ticks.ToString(CultureInfo.InvariantCulture));
        var token = Base64UrlTextEncoder.Encode(_protector.Protect(Encoding.UTF8.GetBytes(payload)));
        return new McpDownloadTicket(token, expires);
    }

    /// <inheritdoc />
    public McpDownloadTicketValidation Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
        {
            return new McpDownloadTicketValidation(McpDownloadTicketValidationResult.Invalid, null);
        }

        byte[] plain;
        try
        {
            plain = _protector.Unprotect(Base64UrlTextEncoder.Decode(token));
        }
        catch
        {
            // Malformed base64, wrong key ring or tampered payload — uniform rejection.
            return new McpDownloadTicketValidation(McpDownloadTicketValidationResult.Invalid, null);
        }

        var parts = Encoding.UTF8.GetString(plain).Split('|');
        if (parts.Length != 5
            || !string.Equals(parts[0], Version, StringComparison.Ordinal)
            || !Guid.TryParseExact(parts[1], "N", out var owner)
            || !Guid.TryParseExact(parts[2], "N", out var backup)
            || !long.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
            || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
        {
            return new McpDownloadTicketValidation(McpDownloadTicketValidationResult.Invalid, null);
        }

        Guid? access = Guid.TryParseExact(parts[3], "N", out var accessId) ? accessId : null;
        var expires = new DateTime(ticks, DateTimeKind.Utc);
        var grant = new McpDownloadTicketGrant(owner, backup, access, expires);
        return expires <= _clock.GetUtcNow().UtcDateTime
            ? new McpDownloadTicketValidation(McpDownloadTicketValidationResult.Expired, grant)
            : new McpDownloadTicketValidation(McpDownloadTicketValidationResult.Valid, grant);
    }
}
