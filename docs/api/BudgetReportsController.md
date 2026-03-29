# BudgetReportsController

Path: `FinanceManager.Web.Controllers.BudgetReportsController`

Purpose:
- Retrieve budget reports, KPIs and exports for the user's budgets.

Key endpoints
- `GET /api/budget/reports/home-kpi` - KPI overview
- `GET /api/budget/reports/{reportId}` - get report data
- `POST /api/budget/reports/export` - export report

Notes
- Reports are read-optimized and rely on posting aggregates. Include examples of query parameters (period, accounts, categories).