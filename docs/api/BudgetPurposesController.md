# BudgetPurposesController

Path: `FinanceManager.Web.Controllers.BudgetPurposesController`

Purpose:
- Manage budget purposes, associate with categories and owners.

Key endpoints
- `GET /api/budget/purposes` - list
- `GET /api/budget/purposes/{id}` - get
- `POST /api/budget/purposes` - create
- `PUT /api/budget/purposes/{id}` - update
- `DELETE /api/budget/purposes/{id}` - delete

Notes
- Used by budget rules and reporting; name uniqueness per owner is enforced.