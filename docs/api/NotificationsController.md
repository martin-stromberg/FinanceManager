# NotificationsController

Path: `FinanceManager.Web.Controllers.NotificationsController`

Purpose:
- Manage user notifications and their preferences.

Key endpoints
- `GET /api/notifications` - list
- `POST /api/notifications` - create (system/admin)
- `PUT /api/notifications/{id}` - update
- `POST /api/notifications/{id}/dismiss` - dismiss

Notes
- Notifications are scoped to owner and may be scheduled for a date/time.