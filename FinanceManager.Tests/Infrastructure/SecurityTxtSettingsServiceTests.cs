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

public sealed class SecurityTxtSettingsServiceTests
{
    private static (SecurityTxtSettingsService service, AppDbContext db) Create(
        string baseAddress = "https://example.com/")
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
    // BuildContentAsync — Canonical from config
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task BuildContent_CanonicalFromConfig()
    {
        var (service, _) = Create(baseAddress: "https://myapp.example.org/");
        await SeedContactAsync(service);

        var result = await service.BuildContentAsync(SecurityTxtFormat.PlainText, CancellationToken.None);

        result.Should().Contain("Canonical: https://myapp.example.org/.well-known/security.txt");
    }

    // ---------------------------------------------------------------------------
    // BuildContentAsync — optional fields omitted when null/empty
    // ---------------------------------------------------------------------------

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
    }

    // ---------------------------------------------------------------------------
    // UpdateAsync — persists changes
    // ---------------------------------------------------------------------------

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
    }
}
