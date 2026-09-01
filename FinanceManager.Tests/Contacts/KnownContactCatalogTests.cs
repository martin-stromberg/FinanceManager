using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure.Contacts;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Contacts;

/// <summary>
/// Covers <see cref="KnownContactCatalog"/>, which loads a JSON file of known counterparties (with wildcard
/// aliases) to auto-suggest a contact for imported statement lines, and its handling of ambiguous alias matches.
/// </summary>
public sealed class KnownContactCatalogTests
{
    /// <summary>Verifies the catalog can parse a JSON definition file (including a string-based enum for the contact type) and match a statement line against a wildcard alias.</summary>
    [Fact]
    public async Task FindMatchAsync_LoadsJsonStringEnumAndMatchesAlias()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(filePath, """
        {
          "contacts": [
            {
              "name": "Amazon",
              "type": "Organization",
              "aliases": [ "AMAZON*" ]
            }
          ]
        }
        """, TestContext.Current.CancellationToken);

        try
        {
            var catalog = new KnownContactCatalog(filePath, NullLogger<KnownContactCatalog>.Instance);

            var match = await catalog.FindMatchAsync(new[] { "AMAZON EU" }, CancellationToken.None);

            Assert.NotNull(match);
            Assert.Equal("Amazon", match!.Name);
            Assert.Equal(ContactType.Organization, match.Type);
            Assert.Contains("AMAZON*", match.Aliases);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>Ensures an ambiguous alias - one that matches more than one catalog entry - yields no match at all, rather than guessing which contact was meant.</summary>
    [Fact]
    public async Task FindMatchAsync_ReturnsNull_WhenMultipleDefinitionsMatch()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(filePath, """
        {
          "contacts": [
            { "name": "Amazon", "aliases": [ "AMAZON*" ] },
            { "name": "Amazon Payments", "aliases": [ "AMAZON*" ] }
          ]
        }
        """, TestContext.Current.CancellationToken);

        try
        {
            var catalog = new KnownContactCatalog(filePath, NullLogger<KnownContactCatalog>.Instance);

            var match = await catalog.FindMatchAsync(new[] { "AMAZON EU" }, CancellationToken.None);

            Assert.Null(match);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
