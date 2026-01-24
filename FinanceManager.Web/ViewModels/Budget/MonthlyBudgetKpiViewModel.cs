using System;
using System.Threading;

namespace FinanceManager.Web.ViewModels.Budget
{
    /// <summary>
    /// ViewModel for the Monthly Budget KPI tile.
    /// </summary>
    public sealed class MonthlyBudgetKpiViewModel
    {
        /// <summary>
        /// Gets or sets a value indicating whether the data has been loaded.
        /// </summary>
        public bool DataLoaded { get; set; } = false;
        /// <summary>
        /// Planned income for the current month.
        /// </summary>
        public decimal PlannedIncome { get; set; }

        /// <summary>
        /// Planned expenses (absolute, positive value) for the current month.
        /// </summary>
        public decimal PlannedExpenseAbs { get; set; }

        /// <summary>
        /// Expected income for the current month (planned + unbudgeted actuals).
        /// </summary>
        public decimal ExpectedIncome { get; set; }

        /// <summary>
        /// Expected expenses (absolute, positive value) for the current month (planned + unbudgeted actuals).
        /// </summary>
        public decimal ExpectedExpenseAbs { get; set; }
        /// <summary>
        /// Unbudgeted actual income in the period.
        /// </summary>
        public decimal UnbudgetedIncome { get; set; }

        /// <summary>
        /// Unbudgeted actual expenses (absolute) in the period.
        /// </summary>
        public decimal UnbudgetedExpenseAbs { get; set; }

        /// <summary>
        /// Actual income for the current month.
        /// </summary>
        public decimal ActualIncome { get; set; }

        /// <summary>
        /// Actual expenses (absolute, positive value) for the current month.
        /// </summary>
        public decimal ActualExpenseAbs { get; set; }

        /// <summary>
        /// Target result (planned income minus planned expenses) for the current month.
        /// </summary>
        public decimal SollErgebnis { get; set; }

        /// <summary>
        /// The month for which the KPI is calculated.
        /// </summary>
        public DateTime Month { get; set; }

        /// <summary>
        /// Loads the monthly budget KPI data from the API and maps it to this ViewModel.
        /// </summary>
        /// <param name="api">The API client to use for data retrieval.</param>
        /// <param name="ct">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task LoadAsync(FinanceManager.Shared.IApiClient api, CancellationToken ct = default)
        {
            var kpiDto = await api.Budgets_GetMonthlyKpiAsync(ct);            
            PlannedIncome = kpiDto.PlannedIncome;
            PlannedExpenseAbs = kpiDto.PlannedExpenseAbs;
            ActualIncome = kpiDto.ActualIncome;
            ActualExpenseAbs = kpiDto.ActualExpenseAbs;
            SollErgebnis = kpiDto.TargetResult;
            ExpectedIncome = kpiDto.ExpectedIncome;
            ExpectedExpenseAbs = kpiDto.ExpectedExpenseAbs;
            UnbudgetedIncome = kpiDto.UnbudgetedIncome;
            UnbudgetedExpenseAbs = kpiDto.UnbudgetedExpenseAbs;
            DataLoaded = true;
        }
    }
}
