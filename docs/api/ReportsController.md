# ReportsController

Path: `FinanceManager.Web.Controllers.ReportsController`

Purpose:
- Reporting endpoints for various financial reports and exports.

Key endpoints
- `GET /api/reports/portfolio` - portfolio report
- `GET /api/reports/transactions` - transaction-level reports
- `POST /api/reports/export` - export selected report

Notes
- Support query parameters for account filters, date ranges, and grouping.