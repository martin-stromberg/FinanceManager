using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using FinanceManager.Web.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Tests.Web.Services;

public sealed class AlphaVantageErrorHandlingTests
{
    [Fact]
    public void Ctor_WhenBaseAddressMissing_SetsDefaultAlphaVantageBaseAddress()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse("{}")));

        _ = new AlphaVantage(client, "test-key");

        client.BaseAddress.Should().Be(new Uri("https://www.alphavantage.co/"));
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_Http400_InvalidApiCall_MapsToInvalidSymbolOrFunction()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse(
            HttpStatusCode.BadRequest,
            "{\"Error Message\":\"Invalid API call. Please retry or visit documentation for TIME_SERIES_DAILY.\"}")))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("BAD", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.InvalidSymbolOrFunction);
        ex.ErrorClassCode.Should().Be("INVALID_SYMBOL_OR_FUNCTION");
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_Http503_EmptyBody_MapsToTransientNetwork()
    {
        var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.TransientNetwork);
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_Http503_NoteBody_MapsToRateLimitAndMasksApiKey()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse(
            HttpStatusCode.ServiceUnavailable,
            "{\"Note\":\"retry later apikey=secret123\"}")))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.RateLimit);
        ex.ProviderRawMessage.Should().Contain("apikey=***");
        ex.ProviderRawMessage.Should().NotContain("secret123");
        sut.LimitExceeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_Http404_EmptyBody_MapsToUnknownProviderError()
    {
        var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.UnknownProviderError);
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_Http503_MalformedJson_FallsBackToStatus_TransientNetwork()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse(HttpStatusCode.ServiceUnavailable, "{\"Error Message\":")))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.TransientNetwork);
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_Http400_MalformedJson_FallsBackToStatus_UnknownProviderError()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse(HttpStatusCode.BadRequest, "{\"Error Message\":")))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.UnknownProviderError);
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_Non2xxBody_WithApiKey_MasksProviderRawMessageAndLogs()
    {
        const string apiKeyInBody = "apikey=secret123";
        var body = $"provider failed: {apiKeyInBody}&symbol=MSFT";
        var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        }))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };
        var logger = new ListLogger<AlphaVantage>();
        var sut = new AlphaVantage(client, "test-key", logger);

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));

        ex.ErrorClass.Should().Be(PriceProviderErrorClass.TransientNetwork);
        ex.ProviderRawMessage.Should().Contain("apikey=***");
        ex.ProviderRawMessage.Should().NotContain("secret123");

        var messages = string.Join(Environment.NewLine, logger.Messages);
        messages.Should().Contain("apikey=***");
        messages.Should().NotContain("secret123");
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_Non2xxBody_ControlCharsAndLongText_SanitizedAndTruncated()
    {
        var body = $"prefix \0 \u0001 apikey=super-secret-key {new string('x', 800)}";
        var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        }))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));

        ex.ErrorClass.Should().Be(PriceProviderErrorClass.TransientNetwork);
        ex.ProviderRawMessage.Should().NotContain("\0");
        ex.ProviderRawMessage.Should().NotContain("\u0001");
        ex.ProviderRawMessage.Should().Contain("apikey=***");
        ex.ProviderRawMessage.Should().NotContain("super-secret-key");
        ex.ProviderRawMessage.Should().NotBeNull();
        ex.ProviderRawMessage!.Length.Should().BeLessThanOrEqualTo(500);
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_InvalidApiCallForDaily_MapsToInvalidSymbolOrFunction()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse("{\"Error Message\":\"Invalid API call. Please retry or visit documentation for TIME_SERIES_DAILY.\"}")))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("BAD", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.InvalidSymbolOrFunction);
        ex.ErrorClassCode.Should().Be("INVALID_SYMBOL_OR_FUNCTION");
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_NoteResponse_MapsToRateLimitAndSetsLimitExceeded()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse("{\"Note\":\"Thank you for using Alpha Vantage!\"}")))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.RateLimit);
        sut.LimitExceeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_InformationResponse_MapsToRateLimitAndSetsLimitExceeded()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse("{\"Information\":\"Our standard API rate limit is 25 requests per day.\"}")))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.RateLimit);
        ex.ErrorClassCode.Should().Be("RATE_LIMIT");
        sut.LimitExceeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_ErrorMessageWithoutInvalidApiCall_MapsToUnknownProviderError()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse("{\"Error Message\":\"Provider failure while handling TIME_SERIES_DAILY request.\"}")))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.UnknownProviderError);
        ex.ErrorClassCode.Should().Be("UNKNOWN_PROVIDER_ERROR");
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_ErrorMessageWithoutTimeSeriesDaily_MapsToUnknownProviderError()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse("{\"Error Message\":\"Invalid API call. Please retry with TIME_SERIES_WEEKLY.\"}")))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.UnknownProviderError);
        ex.ErrorClassCode.Should().Be("UNKNOWN_PROVIDER_ERROR");
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_UnexpectedPayload_MapsToUnknownProviderError()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse("{\"Meta Data\":{}}")))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.UnknownProviderError);
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_Http429_MapsToRateLimit()
    {
        var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{\"Error Message\":\"API rate limit exceeded\"}", Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.RateLimit);
        sut.LimitExceeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_MalformedJson_MapsToUnknownProviderError()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse("{\"Meta Data\":")))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.UnknownProviderError);
        ex.Message.Should().Be("Price provider returned an invalid JSON payload.");
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_LogsSanitizedRequestUrlWithoutApiKey()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse("{\"Error Message\":\"Invalid API call. Please retry or visit documentation for TIME_SERIES_DAILY.\"}")))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };
        var logger = new ListLogger<AlphaVantage>();
        var sut = new AlphaVantage(client, "secret-test-key", logger);

        await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));

        var messages = string.Join(Environment.NewLine, logger.Messages);
        messages.Should().Contain("apikey=***");
        messages.Should().NotContain("secret-test-key");
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_WhenRateLimitAlreadyExceeded_DoesNotPerformHttpCall()
    {
        var handler = new StubHandler(_ => JsonResponse("{\"Note\":\"Limit reached\"}"));
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };
        var sut = new AlphaVantage(client, "test-key");

        await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        handler.CallCount.Should().Be(1);

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.RateLimit);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_HttpRequestException429_MapsToRateLimitAndSanitizesMessage()
    {
        const string rawMessage = "transport failure apikey=secret123";
        var client = new HttpClient(new StubHandler(_ => throw new HttpRequestException(rawMessage, null, HttpStatusCode.TooManyRequests)))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.RateLimit);
        ex.ProviderRawMessage.Should().Contain("apikey=***");
        ex.ProviderRawMessage.Should().NotContain("secret123");
        sut.LimitExceeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_HttpRequestExceptionWithoutStatus_MapsToTransientNetworkAndSanitizesMessage()
    {
        const string rawMessage = "socket reset apikey=secret123";
        var client = new HttpClient(new StubHandler(_ => throw new HttpRequestException(rawMessage)))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.TransientNetwork);
        ex.ProviderRawMessage.Should().Contain("apikey=***");
        ex.ProviderRawMessage.Should().NotContain("secret123");
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_Http503_InformationBody_MapsToRateLimitAndMasksApiKey()
    {
        var client = new HttpClient(new StubHandler(_ => JsonResponse(
            HttpStatusCode.ServiceUnavailable,
            "{\"Information\":\"retry later apikey=secret123\"}")))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var sut = new AlphaVantage(client, "test-key");

        var ex = await Assert.ThrowsAsync<PriceProviderException>(() => sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None));
        ex.ErrorClass.Should().Be(PriceProviderErrorClass.RateLimit);
        ex.ProviderRawMessage.Should().Contain("apikey=***");
        ex.ProviderRawMessage.Should().NotContain("secret123");
        sut.LimitExceeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetTimeSeriesDailyAsync_Enumerate_SkipsInvalidRowsAndReturnsValidRowsOnly()
    {
        var responseBody = """
                           {
                             "Time Series (Daily)": {
                               "invalid-date": {
                                 "1. open": "1",
                                 "2. high": "2",
                                 "3. low": "1",
                                 "4. close": "2",
                                 "5. volume": "10"
                               },
                               "2024-01-03": {
                                 "1. open": "foo",
                                 "2. high": "2",
                                 "3. low": "1",
                                 "4. close": "2",
                                 "5. volume": "10"
                               },
                               "2024-01-04": {
                                 "1. open": "1",
                                 "2. high": "2",
                                 "3. low": "1",
                                 "4. close": "2",
                                 "5. volume": "10"
                               }
                             }
                           }
                           """;
        var client = new HttpClient(new StubHandler(_ => JsonResponse(responseBody)))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };
        var sut = new AlphaVantage(client, "test-key");

        var series = await sut.GetTimeSeriesDailyAsync("MSFT", CancellationToken.None);
        var rows = series!.Enumerate().ToList();

        rows.Should().ContainSingle();
        rows[0].date.Should().Be(new DateTime(2024, 1, 4));
        rows[0].close.Should().Be(2m);
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(handler(request));
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
