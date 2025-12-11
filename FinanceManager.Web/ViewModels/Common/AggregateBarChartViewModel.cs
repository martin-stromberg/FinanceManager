namespace FinanceManager.Web.ViewModels.Common
{
    public sealed class AggregateBarChartViewModel : BaseViewModel
    {
        private readonly HttpClient _http;
        private readonly string _endpointBase;
        private readonly int? _take;
        private readonly int? _maxYearsBack;

        public AggregateBarChartViewModel(IServiceProvider services, string endpointBase, string? title = null, int? take = null, int? maxYearsBack = null)
            : base(services)
        {
            _http = services.GetRequiredService<HttpClient>();
            _endpointBase = endpointBase ?? string.Empty;
            Title = title ?? string.Empty;
            _take = take;
            _maxYearsBack = maxYearsBack;
            Data = new List<TimeSeriesPoint>();
            // defaults matching previous component defaults
            PositiveColor = "#2d6cdf";
            NegativeColor = "#c94";
            AxisColor = "#555";
            HideIntervalSelector = false;
            InitialPeriod = "Month";
            AutoFit = true;
            MaxVisibleLabels = 18;
            Compact = false;
            BarsHeightPx = null;
        }

        public string Title { get; set; }
        public string PositiveColor { get; set; }
        public string NegativeColor { get; set; }
        public string AxisColor { get; set; }
        public int? Take => _take;
        public int? MaxYearsBack => _maxYearsBack;
        public bool HideIntervalSelector { get; set; }
        public string? InitialPeriod { get; set; }
        public bool AutoFit { get; set; }
        public int MaxVisibleLabels { get; set; }
        public bool Compact { get; set; }
        public int? BarsHeightPx { get; set; }

        public bool Loading { get; private set; }

        public bool MultipleYears { get; private set; }

        public IReadOnlyList<TimeSeriesPoint> Data { get; private set; }

        public record TimeSeriesPoint(DateTime PeriodStart, decimal Amount);

        // Load data for a given period (e.g. "Month", "Quarter")
        public async Task LoadAsync(string period)
        {
            if (string.IsNullOrWhiteSpace(_endpointBase)) return;
            Loading = true; RaiseStateChanged();
            try
            {
                var url = _endpointBase.Contains('?') ? $"{_endpointBase}&period={period}" : $"{_endpointBase}?period={period}";
                if (_take.HasValue && _take.Value > 0) url += $"&take={_take.Value}";
                if (_maxYearsBack.HasValue && _maxYearsBack.Value > 0) url += $"&maxYearsBack={_maxYearsBack.Value}";
                var resp = await _http.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    var list = await resp.Content.ReadFromJsonAsync<List<TimeSeriesResponse>>() ?? new List<TimeSeriesResponse>();
                    var pts = list.Select(x => new TimeSeriesPoint(x.PeriodStart, x.Amount)).ToList();
                    Data = pts;
                    MultipleYears = Data.Select(d => d.PeriodStart.Year).Distinct().Skip(1).Any();
                }
                else
                {
                    Data = new List<TimeSeriesPoint>();
                    MultipleYears = false;
                }
            }
            catch
            {
                Data = new List<TimeSeriesPoint>();
                MultipleYears = false;
            }
            finally
            {
                Loading = false; RaiseStateChanged();
            }
        }

        private sealed class TimeSeriesResponse { public DateTime PeriodStart { get; set; } public decimal Amount { get; set; } }
    }
}
