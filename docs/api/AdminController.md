# AdminController

Path: `FinanceManager.Web.Controllers.AdminController`

Purpose:
- Administrative operations: system-wide maintenance, user management helpers, feature toggles.

Common endpoints:
- `GET /api/admin/status` - health/status information
- `POST /api/admin/seed-demo-data` - create demo data for a user (admin-only)
- `POST /api/admin/clear-cache` - clear server-side caches

Notes:
- Endpoints require admin role. Document required authorization and rate limits.