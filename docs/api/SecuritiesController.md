# SecuritiesController

Path: `FinanceManager.Web.Controllers.SecuritiesController`

Purpose:
- Manage securities (instruments), prices, categories.

Key endpoints:
- `GET /api/securities` - list securities
- `GET /api/securities/{id}` - get security details
- `POST /api/securities` - create security
- `PUT /api/securities/{id}` - update security
- `GET /api/securities/{id}/prices` - historical prices

Notes:
- Document price import, AlphaVantage integration and how Security identifiers are matched during classification.
