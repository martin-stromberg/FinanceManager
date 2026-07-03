using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

public class ApiClientSecuritiesTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ApiClientSecuritiesTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new Shared.ApiClient(http);
    }

    private async Task EnsureAuthenticatedAsync(Shared.ApiClient api)
    {
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new Shared.Dtos.Users.RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
    }

    [Fact]
    public async Task Securities_Flow_CRUD_Symbol_Prices_Aggregates_Dividends_Backfill()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        // initial list and count
        var list = await api.Securities_ListAsync();
        list.Should().NotBeNull();
        var cnt = await api.Securities_CountAsync();
        cnt.Should().BeGreaterThanOrEqualTo(0);

        // create
        var createReq = new SecurityRequest
        {
            Name = "Tesla",
            Identifier = "TSLA",
            Description = null,
            AlphaVantageCode = null,
            CurrencyCode = "USD",
            CategoryId = null
        };
        var created = await api.Securities_CreateAsync(createReq);
        created.Should().NotBeNull();
        created!.Name.Should().Be("Tesla");

        // get
        var got = await api.Securities_GetAsync(created.Id);
        got.Should().NotBeNull();
        got!.Id.Should().Be(created.Id);

        // update
        var updateReq = new SecurityRequest
        {
            Name = "Tesla Inc.",
            Identifier = "TSLA",
            Description = "Electric vehicles",
            AlphaVantageCode = null,
            CurrencyCode = "USD",
            CategoryId = null
        };
        var updated = await api.Securities_UpdateAsync(created.Id, updateReq);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Tesla Inc.");

        // set/clear symbol via fake attachment upload (not asserting content, just route)
        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var attachment = await api.Securities_UploadSymbolAsync(created.Id, ms, "logo.png", "image/png", null);
        attachment.Should().NotBeNull();
        var setOk = await api.Securities_SetSymbolAsync(created.Id, attachment.Id);
        setOk.Should().BeTrue();
        var clearOk = await api.Securities_ClearSymbolAsync(created.Id);
        clearOk.Should().BeTrue();

        // aggregates
        var aggs = await api.Securities_GetAggregatesAsync(created.Id, period: "Month", take: 12);
        aggs.Should().NotBeNull();

        // prices
        var prices = await api.Securities_GetPricesAsync(created.Id, skip: 0, take: 10);
        prices.Should().NotBeNull();

        // dividends
        var dividends = await api.Securities_GetDividendsAsync(period: null, take: null);
        dividends.Should().NotBeNull();

        // backfill enqueue
        var info = await api.Securities_EnqueueBackfillAsync(created.Id, DateTime.UtcNow.AddYears(-1), DateTime.UtcNow);
        info.Should().NotBeNull();

        // archive then delete
        var archived = await api.Securities_ArchiveAsync(created.Id);
        archived.Should().BeTrue();
        var deleted = await api.Securities_DeleteAsync(created.Id);
        deleted.Should().BeTrue();
        var gone = await api.Securities_GetAsync(created.Id);
        gone.Should().BeNull();
    }

    /// <summary>
    /// Verifies that importing valid ING CSV data returns expected counters and persists values.
    /// </summary>
    [Fact]
    public async Task Securities_ImportPrices_ShouldReturnExpectedCounters_AndPersistData()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var created = await api.Securities_CreateAsync(new SecurityRequest
        {
            Name = "MSCI World",
            Identifier = "ISIN-WORLD",
            CurrencyCode = "EUR"
        });

        await using (var initialStream = CreateIngCsvStream(
            "01.07.2026 02:00:00;42,61",
            "02.07.2026 02:00:00;42,48"))
        {
            var initial = await api.Securities_ImportPricesAsync(created.Id, initialStream, "prices.csv", "ing", "text/csv");
            initial.Inserted.Should().Be(2);
            initial.Updated.Should().Be(0);
            initial.Unchanged.Should().Be(0);
            initial.Skipped.Should().Be(0);
            initial.Errors.Should().BeEmpty();
        }

        await using (var secondStream = CreateIngCsvStream(
            "01.07.2026 02:00:00;42,61",
            "02.07.2026 02:00:00;44,00",
            "03.07.2026 02:00:00;45,50"))
        {
            var second = await api.Securities_ImportPricesAsync(created.Id, secondStream, "prices.csv", "ing", "text/csv");
            second.Inserted.Should().Be(1);
            second.Updated.Should().Be(1);
            second.Unchanged.Should().Be(1);
            second.Skipped.Should().Be(0);
            second.Errors.Should().BeEmpty();
        }

        var prices = await api.Securities_GetPricesAsync(created.Id, skip: 0, take: 10);
        prices.Should().NotBeNull();
        prices!.Should().HaveCount(3);
        prices.Single(x => x.Date.Date == new DateTime(2026, 7, 1)).Close.Should().Be(42.61m);
        prices.Single(x => x.Date.Date == new DateTime(2026, 7, 2)).Close.Should().Be(44.00m);
        prices.Single(x => x.Date.Date == new DateTime(2026, 7, 3)).Close.Should().Be(45.50m);
    }

    /// <summary>
    /// Verifies that importing to unknown or foreign securities returns NotFound behavior.
    /// </summary>
    [Fact]
    public async Task Securities_ImportPrices_ShouldReturnNotFound_ForUnknownAndForeignSecurity()
    {
        var apiA = CreateClient();
        await EnsureAuthenticatedAsync(apiA);
        var apiB = CreateClient();
        await EnsureAuthenticatedAsync(apiB);

        var createdByA = await apiA.Securities_CreateAsync(new SecurityRequest
        {
            Name = "Private Security",
            Identifier = "ISIN-PRIVATE",
            CurrencyCode = "EUR"
        });

        await using (var unknownStream = CreateIngCsvStream("01.07.2026 02:00:00;42,61"))
        {
            var unknownCall = async () => await apiA.Securities_ImportPricesAsync(Guid.NewGuid(), unknownStream, "prices.csv");
            await unknownCall.Should().ThrowAsync<HttpRequestException>();
        }

        await using (var foreignStream = CreateIngCsvStream("01.07.2026 02:00:00;42,61"))
        {
            var foreignCall = async () => await apiB.Securities_ImportPricesAsync(createdByA.Id, foreignStream, "prices.csv");
            await foreignCall.Should().ThrowAsync<HttpRequestException>();
        }
    }

    /// <summary>
    /// Verifies that import endpoint returns bad request for invalid file, provider, and data-only-invalid rows.
    /// </summary>
    [Fact]
    public async Task Securities_ImportPrices_ShouldReturnBadRequest_ForInvalidInputScenarios()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var created = await api.Securities_CreateAsync(new SecurityRequest
        {
            Name = "Import Validation",
            Identifier = "ISIN-VALIDATION",
            CurrencyCode = "EUR"
        });

        await using (var emptyStream = new MemoryStream(Array.Empty<byte>()))
        {
            var emptyCall = async () => await api.Securities_ImportPricesAsync(created.Id, emptyStream, "empty.csv");
            await emptyCall.Should().ThrowAsync<HttpRequestException>();
            api.LastErrorCode.Should().Be("Err_Invalid_File");
            api.LastError.Should().NotBeNullOrWhiteSpace();
        }

        await using (var unsupportedProviderStream = CreateIngCsvStream("01.07.2026 02:00:00;42,61"))
        {
            var providerCall = async () => await api.Securities_ImportPricesAsync(created.Id, unsupportedProviderStream, "prices.txt", "unsupported-provider", "text/plain");
            await providerCall.Should().ThrowAsync<HttpRequestException>();
            api.LastError.Should().NotBeNullOrWhiteSpace();
        }

        await using (var invalidRowsOnlyStream = CreateIngCsvStream("not-a-date;invalid"))
        {
            var invalidImportCall = async () => await api.Securities_ImportPricesAsync(created.Id, invalidRowsOnlyStream, "prices.csv");
            await invalidImportCall.Should().ThrowAsync<HttpRequestException>();
            api.LastErrorCode.Should().Be("Err_Invalid_Import");
            api.LastError.Should().NotBeNullOrWhiteSpace();
        }
    }

    private static MemoryStream CreateIngCsvStream(params string[] lines)
    {
        var csv = string.Join('\n', new[] { "sep=;", "Zeit;Test Security" }.Concat(lines)) + '\n';
        return new MemoryStream(Encoding.UTF8.GetBytes(csv));
    }
}
