# SecurityCategoriesController

Path: `FinanceManager.Web.Controllers.SecurityCategoriesController`

Purpose:
- Manage security categories used to classify securities.

Key endpoints
- `GET /api/security-categories` - list
- `POST /api/security-categories` - create
- `PUT /api/security-categories/{id}` - update
- `DELETE /api/security-categories/{id}` - delete

Notes
- Categories are owner-scoped and used by UI to group securities.