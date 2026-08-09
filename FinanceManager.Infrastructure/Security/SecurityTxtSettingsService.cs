using FinanceManager.Application.Security;
using FinanceManager.Domain.Security;
using FinanceManager.Shared.Dtos.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FinanceManager.Infrastructure.Security;

/// <summary>
/// Loads, stores and renders security.txt settings.
/// </summary>
public sealed class SecurityTxtSettingsService : ISecurityTxtSettingsService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    /// <summary>Creates a new instance.</summary>
    public SecurityTxtSettingsService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async Task<SecurityTxtSettingsDto> GetAsync(CancellationToken ct)
    {
        var entity = await GetEntityAsync(ct);
        return new SecurityTxtSettingsDto
        {
            Contact = entity.Contact,
            Expires = entity.Expires,
            Encryption = entity.Encryption,
            Acknowledgments = entity.Acknowledgments,
            PreferredLanguages = entity.PreferredLanguages,
            Policy = entity.Policy,
            Hiring = entity.Hiring,
            Canonical = entity.Canonical
        };
    }

    /// <inheritdoc />
    public async Task UpdateAsync(SecurityTxtSettingsUpdateRequest request, CancellationToken ct)
    {
        var entity = await GetEntityAsync(ct);
        entity.Update(new SecurityTxtDirectives(
            request.Contact,
            request.Expires,
            request.Encryption,
            request.Acknowledgments,
            request.PreferredLanguages,
            request.Policy,
            request.Hiring,
            request.Canonical));
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<string?> BuildContentAsync(SecurityTxtFormat format, CancellationToken ct)
    {
        var entity = await GetEntityAsync(ct);
        if (string.IsNullOrWhiteSpace(entity.Contact))
        {
            return null;
        }

        var canonical = BuildCanonical(entity.Canonical);
        return format switch
        {
            SecurityTxtFormat.Markdown => BuildMarkdown(entity, canonical),
            SecurityTxtFormat.Html => BuildHtml(entity, canonical),
            _ => BuildPlainText(entity, canonical)
        };
    }

    private async Task<SecurityTxtSettings> GetEntityAsync(CancellationToken ct)
    {
        var entity = await _db.SecurityTxtSettings.FirstOrDefaultAsync(ct);
        if (entity != null)
        {
            return entity;
        }

        entity = SecurityTxtSettings.CreateUnconfigured();
        _db.SecurityTxtSettings.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    private string BuildCanonical(string? canonical)
    {
        if (!string.IsNullOrWhiteSpace(canonical))
        {
            return canonical.Trim();
        }

        var baseAddress = _configuration["Api:BaseAddress"] ?? "https://localhost:5001/";
        return new Uri(new Uri(baseAddress), "/.well-known/security.txt").ToString();
    }

    private static string BuildPlainText(SecurityTxtSettings entity, string canonical)
        => string.Join("\n", BuildLines(entity, canonical));

    private static string BuildMarkdown(SecurityTxtSettings entity, string canonical)
    {
        var sections = new List<string>();
        sections.Add($"## Contact\n{entity.Contact}");
        sections.Add($"## Expires\n{entity.Expires:yyyy-MM-ddTHH:mm:sszzz}");
        sections.Add($"## Canonical\n{canonical}");
        if (!string.IsNullOrWhiteSpace(entity.Encryption)) sections.Add($"## Encryption\n{entity.Encryption}");
        if (!string.IsNullOrWhiteSpace(entity.Acknowledgments)) sections.Add($"## Acknowledgments\n{entity.Acknowledgments}");
        if (!string.IsNullOrWhiteSpace(entity.PreferredLanguages)) sections.Add($"## Preferred-Languages\n{entity.PreferredLanguages}");
        if (!string.IsNullOrWhiteSpace(entity.Policy)) sections.Add($"## Policy\n{entity.Policy}");
        if (!string.IsNullOrWhiteSpace(entity.Hiring)) sections.Add($"## Hiring\n{entity.Hiring}");
        return string.Join("\n\n", sections);
    }

    private static string BuildHtml(SecurityTxtSettings entity, string canonical)
        => string.Join("", BuildSections(entity, canonical).Select(section => $"<section>{section}</section>"));

    private static IEnumerable<string> BuildLines(SecurityTxtSettings entity, string canonical)
    {
        yield return $"Contact: {entity.Contact}";
        yield return $"Expires: {entity.Expires:yyyy-MM-ddTHH:mm:sszzz}";
        yield return $"Canonical: {canonical}";
        if (!string.IsNullOrWhiteSpace(entity.Encryption)) yield return $"Encryption: {entity.Encryption}";
        if (!string.IsNullOrWhiteSpace(entity.Acknowledgments)) yield return $"Acknowledgments: {entity.Acknowledgments}";
        if (!string.IsNullOrWhiteSpace(entity.PreferredLanguages)) yield return $"Preferred-Languages: {entity.PreferredLanguages}";
        if (!string.IsNullOrWhiteSpace(entity.Policy)) yield return $"Policy: {entity.Policy}";
        if (!string.IsNullOrWhiteSpace(entity.Hiring)) yield return $"Hiring: {entity.Hiring}";
    }

    private static IEnumerable<string> BuildSections(SecurityTxtSettings entity, string canonical)
    {
        yield return $"<h2>Contact</h2><p>{System.Net.WebUtility.HtmlEncode(entity.Contact)}</p>";
        yield return $"<h2>Expires</h2><p>{entity.Expires:yyyy-MM-ddTHH:mm:sszzz}</p>";
        yield return $"<h2>Canonical</h2><p>{System.Net.WebUtility.HtmlEncode(canonical)}</p>";
        if (!string.IsNullOrWhiteSpace(entity.Encryption)) yield return $"<h2>Encryption</h2><p>{System.Net.WebUtility.HtmlEncode(entity.Encryption)}</p>";
        if (!string.IsNullOrWhiteSpace(entity.Acknowledgments)) yield return $"<h2>Acknowledgments</h2><p>{System.Net.WebUtility.HtmlEncode(entity.Acknowledgments)}</p>";
        if (!string.IsNullOrWhiteSpace(entity.PreferredLanguages)) yield return $"<h2>Preferred-Languages</h2><p>{System.Net.WebUtility.HtmlEncode(entity.PreferredLanguages)}</p>";
        if (!string.IsNullOrWhiteSpace(entity.Policy)) yield return $"<h2>Policy</h2><p>{System.Net.WebUtility.HtmlEncode(entity.Policy)}</p>";
        if (!string.IsNullOrWhiteSpace(entity.Hiring)) yield return $"<h2>Hiring</h2><p>{System.Net.WebUtility.HtmlEncode(entity.Hiring)}</p>";
    }
}
