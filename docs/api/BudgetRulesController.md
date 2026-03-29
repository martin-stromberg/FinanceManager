# BudgetRulesController

Path: `FinanceManager.Web.Controllers.BudgetRulesController`

Purpose:
- CRUD for budget rules which define periodic amounts per category/purpose.

Key endpoints
- `GET /api/budget/rules` - list / filter
- `POST /api/budget/rules` - create
- `PUT /api/budget/rules/{id}` - update
- `DELETE /api/budget/rules/{id}` - delete

Notes
- Amount precision: decimal with precision consistent with domain (example: 18,2).