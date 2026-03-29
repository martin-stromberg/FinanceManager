# HomeKpisController

Path: `FinanceManager.Web.Controllers.HomeKpisController`

Purpose:
- Manage dashboard KPIs and favorites shown on user's home page.

Key endpoints
- `GET /api/home-kpis` - list
- `POST /api/home-kpis` - create
- `PUT /api/home-kpis/{id}` - update
- `DELETE /api/home-kpis/{id}` - delete

Notes
- KPIs reference ReportFavorite ids and are ordered per user.