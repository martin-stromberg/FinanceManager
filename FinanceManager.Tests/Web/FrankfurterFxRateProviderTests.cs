using FinanceManager.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text;

namespace FinanceManager.Tests.Web;

/// <summary>
/// Tests of <see cref="FrankfurterFxRateProvider"/>: JSON contract of the Frankfurter API
/// (ECB reference rates, "foreign currency per 1 EUR"), latest vs. dated endpoints,
/// the single retry on transient failures, null-on-failure semantics and the
/// <c>Fx:Enabled</c> kill switch.
/// </summary>
public sealed class FrankfurterFxRateProviderTests
{
    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    private static FrankfurterFxRateProvider NewProvider(
        Func<HttpRequestMessage, HttpResponseMessage> responder, FxOptions? options = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(FrankfurterFxRateProvider.HttpClientName))
            .Returns(() => new HttpClient(new DelegateHandler(responder)));
        return new FrankfurterFxRateProvider(
            factory.Object,
            NullLogger<FrankfurterFxRateProvider>.Instance,
            Options.Create(options ?? new FxOptions()));
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>
    /// The latest endpoint answers the Frankfurter JSON shape and the provider maps it to
    /// an <see cref="FinanceManager.Application.Securities.FxRate"/> tagged "Frankfurter".
    /// </summary>
    [Fact]
    public async Task GetLatestRateAsync_ParsesFrankfurterShape()
    {
        HttpRequestMessage? seen = null;
        var provider = NewProvider(req =>
        {
            seen = req;
            return Json("{\"amount\":1.0,\"base\":\"EUR\",\"date\":\"2024-01-15\",\"rates\":{\"USD\":1.09}}");
        });

        var rate = await provider.GetLatestRateAsync("USD", CancellationToken.None);

        Assert.NotNull(rate);
        Assert.Equal(new DateTime(2024, 1, 15), rate.Date);
        Assert.Equal(1.09m, rate.RatePerEur);
        Assert.Equal("Frankfurter", rate.Source);
        Assert.NotNull(seen);
        Assert.Equal("https://api.frankfurter.dev/latest?from=EUR&to=USD", seen!.RequestUri!.ToString());
    }

    /// <summary>
    /// A dated lookup hits the <c>/{yyyy-MM-dd}</c> endpoint; the provider-returned date is
    /// authoritative (the API may answer with the previous business day).
    /// </summary>
    [Fact]
    public async Task GetRateAsync_UsesDatedEndpoint_AndReturnsPayloadDate()
    {
        HttpRequestMessage? seen = null;
        var provider = NewProvider(req =>
        {
            seen = req;
            return Json("{\"amount\":1.0,\"base\":\"EUR\",\"date\":\"2024-01-12\",\"rates\":{\"USD\":1.08}}");
        });

        var rate = await provider.GetRateAsync("usd", new DateTime(2024, 1, 14), CancellationToken.None);

        Assert.NotNull(rate);
        Assert.Equal(new DateTime(2024, 1, 12), rate.Date);
        Assert.Equal(1.08m, rate.RatePerEur);
        Assert.Equal("https://api.frankfurter.dev/2024-01-14?from=EUR&to=USD", seen!.RequestUri!.ToString());
    }

    /// <summary>
    /// A transient HTTP failure is retried exactly once before the provider gives up with
    /// <c>null</c> — it never propagates request errors.
    /// </summary>
    [Fact]
    public async Task GetRateAsync_ServerError_RetriesOnce_ThenNull()
    {
        var calls = 0;
        var provider = NewProvider(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var rate = await provider.GetRateAsync("USD", new DateTime(2024, 1, 15), CancellationToken.None);

        Assert.Null(rate);
        Assert.Equal(2, calls);
    }

    /// <summary>
    /// A payload without the requested currency (or a malformed body) maps to <c>null</c>.
    /// </summary>
    [Fact]
    public async Task GetRateAsync_PayloadWithoutCurrency_ReturnsNull()
    {
        var provider = NewProvider(_ => Json("{\"amount\":1.0,\"base\":\"EUR\",\"date\":\"2024-01-15\",\"rates\":{\"CHF\":0.94}}"));

        var rate = await provider.GetRateAsync("USD", new DateTime(2024, 1, 15), CancellationToken.None);

        Assert.Null(rate);
    }

    /// <summary>
    /// <c>Fx:Enabled = false</c> disables the provider entirely — no request is issued and
    /// the result is <c>null</c>.
    /// </summary>
    [Fact]
    public async Task GetRateAsync_Disabled_ReturnsNull_WithoutHttpCall()
    {
        var calls = 0;
        var provider = NewProvider(_ =>
        {
            calls++;
            return Json("{}");
        }, new FxOptions { Enabled = false });

        var rate = await provider.GetRateAsync("USD", new DateTime(2024, 1, 15), CancellationToken.None);

        Assert.Null(rate);
        Assert.Equal(0, calls);
    }
}
