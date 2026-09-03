using System;
using System.Threading;
using System.Threading.Tasks;
using FinanceManager.Application.Security;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Security;
using FinanceManager.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FinanceManager.Tests.Infrastructure;

/// <summary>
/// Covers <see cref="SecurityTxtSettingsService"/>: rendering the admin-configured security contact settings into an
/// RFC 9116 <c>security.txt</c> document in its plain-text, Markdown, and HTML representations, resolving the
/// mandatory Canonical URL from either the persisted setting or a configured API base address, omitting optional
/// fields the admin left blank, and the get/update persistence round trip including its validation rules.
/// </summary>
public sealed class SecurityTxtSettingsServiceTests
{
    private static (SecurityTxtSettingsService service, AppDbContext db) Create(
        string? baseAddress = "https://example.com/")
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string?>("Api:BaseAddress", baseAddress)
            })
            .Build();

        var service = new SecurityTxtSettingsService(db, config);
        return (service, db);
    }

    private static async Task SeedContactAsync(SecurityTxtSettingsService service, string contact = "mailto:security@example.com")
    {
        var request = SecurityTxtSettingsTestData.ValidRequest(contact: contact);
        await service.UpdateAsync(request, CancellationToken.None);
    }

    // ---------------------------------------------------------------------------
    // BuildContentAsync — PlainText
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the plain-text output matches the RFC 9116 field format (e.g. "Contact:", "Expires:",
    /// "Canonical:") - this is the format actually served at <c>/.well-known/security.txt</c>, so its shape is
    /// dictated by the spec, not by the application's own conventions.
    /// </summary>
    [Fact]
    public async Task BuildContent_PlainText_ReturnsRfc9116Format()
    {
        var (service, _) = Create();
        await SeedContactAsync(service);

        var result = await service.BuildContentAsync(SecurityTxtFormat.PlainText, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Contain("Contact: mailto:security@example.com");
        result.Should().Contain("Expires:");
        result.Should().Contain("Canonical: https://example.com/.well-known/security.txt");
    }

    // ---------------------------------------------------------------------------
    // BuildContentAsync — Markdown
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the Markdown rendering uses "##" headings for each field and, importantly, contains no raw
    /// HTML markup - this output is meant for embedding in the human-facing help/documentation pages, not the
    /// machine-readable RFC 9116 endpoint.
    /// </summary>
    [Fact]
    public async Task BuildContent_Markdown_ReturnsMdHeadings()
    {
        var (service, _) = Create();
        await SeedContactAsync(service);

        var result = await service.BuildContentAsync(SecurityTxtFormat.Markdown, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Contain("## Contact");
        result.Should().Contain("mailto:security@example.com");
        result.Should().NotContain("<html");
        result.Should().NotContain("<section");
    }

    // ---------------------------------------------------------------------------
    // BuildContentAsync — Html
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the HTML rendering wraps the content in a <c>&lt;section&gt;</c> with an <c>&lt;h2&gt;</c>
    /// heading per field - the third of the three renderings, used for embedding directly into an HTML help page.
    /// </summary>
    [Fact]
    public async Task BuildContent_Html_ReturnsHtmlSection()
    {
        var (service, _) = Create();
        await SeedContactAsync(service);

        var result = await service.BuildContentAsync(SecurityTxtFormat.Html, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Contain("<section>");
        result.Should().Contain("<h2>Contact</h2>");
    }

    // ---------------------------------------------------------------------------
    // BuildContentAsync — Canonical fallback from config
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that when the admin has not set an explicit Canonical URL, it is derived from the configured
    /// <c>Api:BaseAddress</c> plus the well-known path - RFC 9116 requires a Canonical URL, so the service must be
    /// able to produce a sensible default instead of always requiring manual configuration.
    /// </summary>
    [Fact]
    public async Task BuildContent_UsesApiBaseAddressFallback_WhenCanonicalEmpty()
    {
        var (service, _) = Create(baseAddress: "https://myapp.example.org/");
        await service.UpdateAsync(SecurityTxtSettingsTestData.ValidRequest(canonical: null), CancellationToken.None);

        var result = await service.BuildContentAsync(SecurityTxtFormat.PlainText, CancellationToken.None);

        result.Should().Contain("Canonical: https://myapp.example.org/.well-known/security.txt");
    }

    /// <summary>
    /// Verifies that an explicitly persisted Canonical URL takes precedence over the API-base-address fallback -
    /// an admin who has deliberately set a custom canonical (e.g. a different host serving the file) must not have
    /// it silently overridden by the derived default.
    /// </summary>
    [Fact]
    public async Task BuildContent_UsesPersistedCanonical_WhenSet()
    {
        var (service, _) = Create(baseAddress: "https://myapp.example.org/");
        await service.UpdateAsync(SecurityTxtSettingsTestData.ValidRequestWithCanonical("https://security.example.org/.well-known/security.txt"), CancellationToken.None);

        var result = await service.BuildContentAsync(SecurityTxtFormat.PlainText, CancellationToken.None);

        result.Should().Contain("Canonical: https://security.example.org/.well-known/security.txt");
        result.Should().NotContain("Canonical: https://myapp.example.org/.well-known/security.txt");
    }

    /// <summary>
    /// Verifies that when neither a persisted Canonical URL nor a configured <c>Api:BaseAddress</c> is available,
    /// building the content throws with a message naming the missing configuration key - a mandatory RFC 9116 field
    /// cannot be silently omitted, and a misconfigured deployment should fail loudly rather than serve an invalid
    /// security.txt.
    /// </summary>
    [Fact]
    public async Task BuildContent_Throws_WhenCanonicalEmpty_AndApiBaseAddressMissing()
    {
        var (service, _) = Create(baseAddress: null);
        await service.UpdateAsync(SecurityTxtSettingsTestData.ValidRequest(canonical: null), CancellationToken.None);

        var act = () => service.BuildContentAsync(SecurityTxtFormat.PlainText, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Api:BaseAddress*");
    }

    // ---------------------------------------------------------------------------
    // BuildContentAsync — optional fields omitted when null/empty
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that optional RFC 9116 fields (Encryption, Acknowledgments, Preferred-Languages, Policy, Hiring)
    /// left blank by the admin are omitted from the output entirely rather than rendered as empty lines - an
    /// empty "Policy:" line would still be a spec-valid but confusing/incomplete-looking entry.
    /// </summary>
    [Fact]
    public async Task BuildContent_OptionalFieldsOmitted_WhenEmpty()
    {
        var (service, _) = Create();
        await service.UpdateAsync(SecurityTxtSettingsTestData.MinimalRequest(), CancellationToken.None);

        var result = await service.BuildContentAsync(SecurityTxtFormat.PlainText, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Contain("Contact:");
        result.Should().NotContain("Encryption:");
        result.Should().NotContain("Acknowledgments:");
        result.Should().NotContain("Preferred-Languages:");
        result.Should().NotContain("Policy:");
        result.Should().NotContain("Hiring:");
    }

    // ---------------------------------------------------------------------------
    // BuildContentAsync — returns null when Contact empty
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that when the settings have never been configured (Contact is still empty), building the content
    /// returns null instead of a partially-valid document - Contact is the one mandatory field a security.txt
    /// cannot exist without, so an unconfigured instance should serve nothing rather than a broken file.
    /// </summary>
    [Fact]
    public async Task BuildContent_ReturnsNull_WhenContactEmpty()
    {
        var (service, _) = Create();
        // No seed — entity is created as unconfigured (empty Contact)

        var result = await service.BuildContentAsync(SecurityTxtFormat.PlainText, CancellationToken.None);

        result.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // GetAsync — returns mapped DTO
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that every field on the update request round-trips through <c>GetAsync</c> unchanged - a basic
    /// mapping-completeness check so a newly added field cannot silently be dropped by the DTO mapping.
    /// </summary>
    [Fact]
    public async Task GetAsync_ReturnsMappedDto()
    {
        var (service, _) = Create();
        var request = SecurityTxtSettingsTestData.ValidRequest();
        await service.UpdateAsync(request, CancellationToken.None);

        var dto = await service.GetAsync(CancellationToken.None);

        dto.Should().NotBeNull();
        dto.Contact.Should().Be(request.Contact);
        dto.Expires.Should().Be(request.Expires);
        dto.Encryption.Should().Be(request.Encryption);
        dto.Acknowledgments.Should().Be(request.Acknowledgments);
        dto.PreferredLanguages.Should().Be(request.PreferredLanguages);
        dto.Policy.Should().Be(request.Policy);
        dto.Hiring.Should().Be(request.Hiring);
        dto.Canonical.Should().Be(request.Canonical);
    }

    // ---------------------------------------------------------------------------
    // UpdateAsync — persists changes
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a second <c>UpdateAsync</c> call overwrites the previously saved settings (the singleton
    /// security.txt configuration is updated in place, not appended as a new record) and that an unset Canonical
    /// on the second update correctly reverts to null rather than retaining the earlier value.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var (service, _) = Create();
        var initial = SecurityTxtSettingsTestData.ValidRequest(contact: "mailto:initial@example.com");
        await service.UpdateAsync(initial, CancellationToken.None);

        var updated = SecurityTxtSettingsTestData.ValidRequest(contact: "mailto:updated@example.com");
        await service.UpdateAsync(updated, CancellationToken.None);

        var dto = await service.GetAsync(CancellationToken.None);

        dto.Contact.Should().Be("mailto:updated@example.com");
        dto.Canonical.Should().BeNull();
    }

    /// <summary>
    /// Verifies that an explicitly supplied Canonical URL is persisted and returned as-is on the next
    /// <c>GetAsync</c> call.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_PersistsCanonical()
    {
        var (service, _) = Create();
        var request = SecurityTxtSettingsTestData.ValidRequestWithCanonical("https://security.example.com/.well-known/security.txt");

        await service.UpdateAsync(request, CancellationToken.None);

        var dto = await service.GetAsync(CancellationToken.None);

        dto.Canonical.Should().Be("https://security.example.com/.well-known/security.txt");
    }

    /// <summary>
    /// Verifies that saving an "Expires" timestamp in the past is rejected with
    /// <see cref="ArgumentOutOfRangeException"/> - RFC 9116 recommends a future expiry, and accepting a past date
    /// would immediately publish an already-expired (and therefore untrustworthy) security.txt.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Throws_WhenExpiresInPast()
    {
        var (service, _) = Create();
        var request = SecurityTxtSettingsTestData.ValidRequest(expires: DateTimeOffset.UtcNow.AddMinutes(-5));

        var act = () => service.UpdateAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*Expires must be in the future.*");
    }
}
