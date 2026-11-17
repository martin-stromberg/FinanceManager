using FinanceManager.Application.Mcp;
using FinanceManager.Web.Mcp;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of the stateless <see cref="McpDownloadTicketService"/> (DataProtection-signed
/// ticket payload): issue/validate round-trip incl. owner+backup+access binding, rejection
/// of tampered, wrong-purpose and malformed tokens, and the expiry path.
/// </summary>
public sealed class McpDownloadTicketServiceTests
{
    private static IDataProtectionProvider NewProvider()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
    }

    private static McpDownloadTicketService NewService(IDataProtectionProvider? provider = null, TimeProvider? clock = null)
        => new(provider ?? NewProvider(), clock ?? TimeProvider.System);

    /// <summary>Test case.</summary>
    [Fact]
    public void Issue_ThenValidate_ReturnsGrantBoundToOwnerAndBackup()
    {
        var owner = Guid.NewGuid();
        var backup = Guid.NewGuid();
        var access = Guid.NewGuid();
        var sut = NewService();

        var ticket = sut.Issue(owner, backup, access);
        var validation = sut.Validate(ticket.Token);

        Assert.Equal(McpDownloadTicketValidationResult.Valid, validation.Result);
        Assert.NotNull(validation.Grant);
        var grant = validation.Grant!;
        Assert.Equal(owner, grant.OwnerUserId);
        Assert.Equal(backup, grant.BackupId);
        Assert.Equal(access, grant.McpAccessId);
        Assert.Equal(ticket.ExpiresUtc, grant.ExpiresUtc);
        Assert.True(ticket.ExpiresUtc > DateTime.UtcNow);
        // URL-safety: the token is a plain path segment (no +, / or = characters).
        Assert.Matches("^[A-Za-z0-9_-]+$", ticket.Token);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public void Validate_RejectsTamperedWrongPurposeAndMalformedTokens()
    {
        var provider = NewProvider();
        var sut = NewService(provider);
        var ticket = sut.Issue(Guid.NewGuid(), Guid.NewGuid(), null);

        // Bit flip inside the protected payload must fail authentication.
        var chars = ticket.Token.ToCharArray();
        chars[^3] = chars[^3] == 'A' ? 'B' : 'A';
        Assert.Equal(McpDownloadTicketValidationResult.Invalid, sut.Validate(new string(chars)).Result);

        // A syntactically identical payload protected under a different purpose is invalid.
        var wrongPurpose = provider.CreateProtector("FinanceManager.Mcp.DownloadTicket.OTHER");
        var payload = $"v1|{Guid.NewGuid():N}|{Guid.NewGuid():N}|-|{DateTime.UtcNow.AddMinutes(5).Ticks}";
        var badToken = Base64UrlTextEncoder.Encode(wrongPurpose.Protect(Encoding.UTF8.GetBytes(payload)));
        Assert.Equal(McpDownloadTicketValidationResult.Invalid, sut.Validate(badToken).Result);

        // Malformed, empty and oversized inputs are invalid, never exceptions.
        Assert.Equal(McpDownloadTicketValidationResult.Invalid, sut.Validate("not-a-token").Result);
        Assert.Equal(McpDownloadTicketValidationResult.Invalid, sut.Validate(string.Empty).Result);
        Assert.Equal(McpDownloadTicketValidationResult.Invalid, sut.Validate(new string('x', 5000)).Result);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public void Validate_DistinguishesExpiredFromInvalid()
    {
        var owner = Guid.NewGuid();
        var backup = Guid.NewGuid();
        var sut = NewService();

        var ticket = sut.Issue(owner, backup, null, lifetime: TimeSpan.FromMinutes(-1));
        var validation = sut.Validate(ticket.Token);

        // Expired tickets still expose their grant for audit attribution.
        Assert.Equal(McpDownloadTicketValidationResult.Expired, validation.Result);
        Assert.NotNull(validation.Grant);
        Assert.Equal(backup, validation.Grant!.BackupId);
    }
}
