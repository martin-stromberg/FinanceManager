using FinanceManager.Application.Mcp;
using FinanceManager.Application.Security;
using FinanceManager.Domain.Mcp;
using FinanceManager.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace FinanceManager.Web.Mcp;

/// <summary>Options of the MCP PAT authentication scheme (marker type).</summary>
public sealed class McpAuthOptions : AuthenticationSchemeOptions { }

/// <summary>
/// PAT authentication handler of the <c>/mcp</c> path (F-0102): extracts
/// <c>Authorization: Bearer fm_mcp_&lt;hint&gt;_&lt;secret&gt;</c>, resolves the access via the
/// non-secret hint, verifies the secret against the stored <c>pbkdf2|…</c> hash and re-checks
/// status, expiry, revocation and IP binding on every request (server-side immediate check,
/// sicherheitskonzept.md §3). All failures produce a uniform 401 without cause differentiation;
/// failures on the unauthenticated path feed the <see cref="IIpBlockService"/> counters.
/// </summary>
public sealed class McpAuthHandler : AuthenticationHandler<McpAuthOptions>
{
    private const string LastUsedThrottle = "mcp.lastUsedWritten";
    private static readonly TimeSpan LastUsedInterval = TimeSpan.FromMinutes(1);

    private readonly AppDbContext _db;
    private readonly IMcpTokenIssuer _tokenIssuer;
    private readonly IMcpTokenHasher _tokenHasher;
    private readonly IIpBlockService _ipBlock;
    private readonly IMcpAuditWriter _auditWriter;

    /// <summary>Creates the handler.</summary>
    public McpAuthHandler(
        IOptionsMonitor<McpAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext db,
        IMcpTokenIssuer tokenIssuer,
        IMcpTokenHasher tokenHasher,
        IIpBlockService ipBlock,
        IMcpAuditWriter auditWriter)
        : base(options, logger, encoder)
    {
        _db = db;
        _tokenIssuer = tokenIssuer;
        _tokenHasher = tokenHasher;
        _ipBlock = ipBlock;
        _auditWriter = auditWriter;
    }

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header)
            || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var remoteIp = Context.Connection.RemoteIpAddress?.ToString();
        var token = header["Bearer ".Length..].Trim();
        if (!_tokenIssuer.TryParse(token, out var hint, out var secret))
        {
            await RecordFailureAsync(remoteIp, null, Context.RequestAborted);
            return AuthenticateResult.Fail("Invalid token");
        }

        var access = await _db.McpAccesses.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccessKeyHint == hint, Context.RequestAborted);
        if (access is null || !_tokenHasher.Verify(secret, access.TokenHash))
        {
            await RecordFailureAsync(remoteIp, hint, Context.RequestAborted);
            return AuthenticateResult.Fail("Invalid token");
        }

        // Status/expiry/revocation/IP binding — every request, uniform 401.
        if (!access.IsCurrentlyUsable(DateTime.UtcNow)
            || !McpIpRangeMatcher.IsAllowed(access.AllowedIpRanges, Context.Connection.RemoteIpAddress))
        {
            try
            {
                await _auditWriter.WriteAsync(new McpAuditWriteRequest(
                    McpAuditCategory.SecurityEvent, "AuthFailed", "Denied",
                    OwnerUserId: access.OwnerUserId, McpAccessId: access.Id, McpAccessName: access.Name,
                    Context: "McpEndpoint", SourceIp: remoteIp), Context.RequestAborted);
            }
            catch (Exception ex)
            {
                // The denial is the effect — an audit failure must not turn a uniform 401
                // into a 500; the missing record surfaces via the chain gap check.
                Logger.LogWarning(ex, "Failed to write MCP security audit record for access {AccessId}", access.Id);
            }
            return AuthenticateResult.Fail("Invalid token");
        }

        // Throttled LastUsedUtc update (~1/min) — tracked via Items to avoid double writes.
        if ((access.LastUsedUtc is null || access.LastUsedUtc.Value < DateTime.UtcNow - LastUsedInterval)
            && !Context.Items.ContainsKey(LastUsedThrottle))
        {
            Context.Items[LastUsedThrottle] = true;
            await _db.McpAccesses.Where(a => a.Id == access.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.LastUsedUtc, DateTime.UtcNow), Context.RequestAborted);
        }

        Context.Items[McpAuthenticationDefaults.McpAccessItemKey] = access;

        var claims = new[]
        {
            new Claim(McpAuthenticationDefaults.McpAccessIdClaim, access.Id.ToString()),
            new Claim(McpAuthenticationDefaults.OwnerUserIdClaim, access.OwnerUserId.ToString()),
            new Claim(ClaimTypes.Name, access.Name)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    /// <inheritdoc />
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // Uniform 401 without cause differentiation (sicherheitskonzept.md §3.4).
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        var payload = new { origin = "API_Mcp", code = "Err_Mcp_Unauthorized", message = "MCP authentication failed." };
        await Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    private async Task RecordFailureAsync(string? remoteIp, string? hint, CancellationToken ct)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(remoteIp))
            {
                await _ipBlock.RegisterUnknownUserFailureAsync(remoteIp, ct);
            }
            await _auditWriter.WriteAsync(new McpAuditWriteRequest(
                McpAuditCategory.SecurityEvent, "AuthFailed", "Denied",
                Context: "McpEndpoint", SourceIp: remoteIp,
                PayloadJson: hint is null ? null : $"{{\"hint\":\"{hint}\"}}"), ct);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to record MCP authentication failure");
        }
    }
}
