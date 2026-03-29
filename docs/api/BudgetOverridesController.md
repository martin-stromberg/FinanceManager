# BudgetOverridesController

Path: `FinanceManager.Web.Controllers.BudgetOverridesController`

Purpose:
- Manage budget overrides for specific purposes and months.

Key endpoints
- `GET /api/budget/overrides` - list overrides
- `POST /api/budget/overrides` - create override
- `PUT /api/budget/overrides/{id}` - update override
- `DELETE /api/budget/overrides/{id}` - delete override

Example request (create)
```json
POST /api/budget/overrides
{
  "budgetPurposeId": "...",
  "periodYear": 2026,
  "periodMonth": 4,
  "amount": "250.00"
}
```

Notes
- Overrides are scoped to owner user and unique per (purpose, year, month).