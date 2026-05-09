using System.Net;
using System.Net.Http;
using System.Text;
using FinanceManager.Application;
using FinanceManager.Web.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Web.Services;

public sealed class AlphaVantagePriceProviderRetryTests
{
    [Fact]
    public async Task GetDailyPricesAsync_NoApiKeyConfigured_ThrowsInvalidOperationException()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(
            JsonResponse(HttpStatusCode.OK, ValidTimeSeriesResponseJson())));
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantagePriceProvider(
            new StubAlphaVantageKeyResolver(null),
            new StubCurrentUserService { IsAuthenticated = false },
            new StubHttpClientFactory(client),
            NullLogger<AlphaVantage>.Instance);

        var act = () => sut.GetDailyPricesAsync(
            "MSFT",
            new DateTime(2024, 01, 01),
            new DateTime(2024, 01, 31),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task GetDailyPricesAsync_Transient503_ThenSuccess_RetriesAndReturnsData()
    {
        var handler = new ScriptedHandler((attempt, _) => Task.FromResult(
            attempt == 1
                ? JsonResponse(HttpStatusCode.ServiceUnavailable, "{\"message\":\"temporary outage\"}")
                : JsonResponse(HttpStatusCode.OK, ValidTimeSeriesResponseJson())));

        var sut = CreateSut(handler);

        var result = await sut.GetDailyPricesAsync(
            "MSFT",
            new DateTime(2024, 01, 01),
            new DateTime(2024, 01, 31),
            CancellationToken.None);

        handler.CallCount.Should().Be(2);
        result.Should().ContainSingle();
        result[0].date.Should().Be(new DateTime(2024, 01, 05));
        result[0].close.Should().Be(101.23m);
    }

    [Fact]
    public async Task GetDailyPricesAsync_Transient503_Always_RetryExhausted_ThrowsTransientNetwork()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(
            JsonResponse(HttpStatusCode.ServiceUnavailable, "{\"message\":\"temporary outage\"}")));
        var sut = CreateSut(handler);

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetDailyPricesAsync(
            "MSFT",
            new DateTime(2024, 01, 01),
            new DateTime(2024, 01, 31),
            CancellationToken.None));

        ex.ErrorClass.Should().Be(PriceProviderErrorClass.TransientNetwork);
        handler.CallCount.Should().Be(4);
    }

    [Fact]
    public async Task GetDailyPricesAsync_RateLimitNote_NoRetry_ThrowsRateLimit()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(
            JsonResponse(HttpStatusCode.OK, "{\"Note\":\"Thank you for using Alpha Vantage\"}")));
        var sut = CreateSut(handler);

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetDailyPricesAsync(
            "MSFT",
            new DateTime(2024, 01, 01),
            new DateTime(2024, 01, 31),
            CancellationToken.None));

        ex.ErrorClass.Should().Be(PriceProviderErrorClass.RateLimit);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDailyPricesAsync_Http429_NoRetry_ThrowsRateLimit()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(
            JsonResponse(HttpStatusCode.TooManyRequests, "{\"Error Message\":\"API rate limit exceeded\"}")));
        var sut = CreateSut(handler);

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetDailyPricesAsync(
            "MSFT",
            new DateTime(2024, 01, 01),
            new DateTime(2024, 01, 31),
            CancellationToken.None));

        ex.ErrorClass.Should().Be(PriceProviderErrorClass.RateLimit);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDailyPricesAsync_InvalidApiCall_NoRetry_ThrowsInvalidSymbolOrFunction()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(
            JsonResponse(HttpStatusCode.OK, "{\"Error Message\":\"Invalid API call. Please retry or visit documentation for TIME_SERIES_DAILY.\"}")));
        var sut = CreateSut(handler);

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetDailyPricesAsync(
            "MSFT",
            new DateTime(2024, 01, 01),
            new DateTime(2024, 01, 31),
            CancellationToken.None));

        ex.ErrorClass.Should().Be(PriceProviderErrorClass.InvalidSymbolOrFunction);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDailyPricesAsync_TransientTransportException_ThenSuccess_RetriesAndReturnsData()
    {
        var handler = new ScriptedHandler((attempt, _) =>
            attempt == 1
                ? Task.FromException<HttpResponseMessage>(new HttpRequestException("transport error", null, HttpStatusCode.ServiceUnavailable))
                : Task.FromResult(JsonResponse(HttpStatusCode.OK, ValidTimeSeriesResponseJson())));
        var sut = CreateSut(handler);

        var result = await sut.GetDailyPricesAsync(
            "MSFT",
            new DateTime(2024, 01, 01),
            new DateTime(2024, 01, 31),
            CancellationToken.None);

        handler.CallCount.Should().Be(2);
        result.Should().ContainSingle();
        result[0].date.Should().Be(new DateTime(2024, 01, 05));
        result[0].close.Should().Be(101.23m);
    }

    [Fact]
    public async Task GetDailyPricesAsync_TimeoutAlways_RetryExhausted_MapsToTransientNetwork()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromException<HttpResponseMessage>(
            new TaskCanceledException("request timed out")));
        var sut = CreateSut(handler);

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetDailyPricesAsync(
            "MSFT",
            new DateTime(2024, 01, 01),
            new DateTime(2024, 01, 31),
            CancellationToken.None));

        ex.ErrorClass.Should().Be(PriceProviderErrorClass.TransientNetwork);
        handler.CallCount.Should().Be(4);
    }

    private static AlphaVantagePriceProvider CreateSut(ScriptedHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        return new AlphaVantagePriceProvider(
            new StubAlphaVantageKeyResolver("test-key"),
            new StubCurrentUserService { IsAuthenticated = false },
            new StubHttpClientFactory(client),
            NullLogger<AlphaVantage>.Instance);
    }

    private static string ValidTimeSeriesResponseJson()
        => """
           {
             "Time Series (Daily)": {
               "2024-01-05": {
                 "1. open": "100.00",
                 "2. high": "102.00",
                 "3. low": "99.50",
                 "4. close": "101.23",
                 "5. volume": "1000"
               }
             }
           }
           """;

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class ScriptedHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        private readonly Func<int, CancellationToken, Task<HttpResponseMessage>> _handler = handler;

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return _handler(CallCount, cancellationToken);
        }
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubAlphaVantageKeyResolver(string? key) : IAlphaVantageKeyResolver
    {
        public Task<string?> GetForUserAsync(Guid userId, CancellationToken ct) => Task.FromResult<string?>(key);
        public Task<string?> GetSharedAsync(CancellationToken ct) => Task.FromResult<string?>(key);
    }

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; init; } = Guid.NewGuid();
        public string? PreferredLanguage { get; init; }
        public bool IsAuthenticated { get; init; }
        public bool IsAdmin { get; init; }
    }
}
