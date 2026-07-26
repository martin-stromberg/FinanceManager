using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure.Contacts;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Contacts;

public sealed class KnownContactCatalogTests
{
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
        """);

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
        """);

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
