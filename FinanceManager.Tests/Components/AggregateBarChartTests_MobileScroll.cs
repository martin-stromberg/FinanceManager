using Bunit;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http;
using System.Text;

namespace FinanceManager.Tests.Components;

public sealed class AggregateBarChartTests_MobileScroll : BunitContext
{
    [Fact]
    public void Chart_RendersScrollableBarTrack_WhenManyBarsExist()
    {
        Services.AddSingleton<IStringLocalizer<AggregateBarChart>, TestStringLocalizer<AggregateBarChart>>();
        Services.AddSingleton(new HttpClient(new AggregateChartHandler()) { BaseAddress = new Uri("http://localhost") });

        var viewModel = new AggregateBarChartViewModel(Services, "/api/test/aggregate")
        {
            HideIntervalSelector = true,
            InitialPeriod = "Month"
        };

        RenderFragment fragment = builder =>
        {
            builder.OpenComponent(0, typeof(AggregateBarChart));
            builder.AddAttribute(1, "ViewModel", viewModel);
            builder.CloseComponent();
        };

        var cut = Render(fragment);

        cut.WaitForAssertion(() =>
        {
            var barsScroll = cut.Find(".bars-scroll");
            var barsTrack = cut.Find(".bars-track");
            Assert.NotNull(barsScroll);
            Assert.Contains("min-width:", barsTrack.GetAttribute("style"));
        });
    }

    private sealed class AggregateChartHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var points = Enumerable.Range(0, 24)
                .Select(i =>
                {
                    var pointDate = start.AddMonths(i);
                    return $$"""{"periodStart":"{{pointDate:O}}","amount":{{(i + 1) * 10}}}""";
                });
            var json = "[" + string.Join(",", points) + "]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class TestStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, false);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(name, arguments), false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();

        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }
}
