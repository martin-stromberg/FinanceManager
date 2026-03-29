# BudgetCategoriesController

Path: `FinanceManager.Web.Controllers.BudgetCategoriesController`

Purpose:
- CRUD operations for budget categories.

Key endpoints
- `GET /api/budget/categories` - list
- `POST /api/budget/categories` - create
- `PUT /api/budget/categories/{id}` - update
- `DELETE /api/budget/categories/{id}` - delete

Notes
- Name uniqueness per owner enforced. Validation returns 400 with errors.