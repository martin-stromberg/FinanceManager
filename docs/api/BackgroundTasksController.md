# BackgroundTasksController

Path: `FinanceManager.Web.Controllers.BackgroundTasksController`

Purpose:
- Query and manage long-running background tasks (status, cancel, list recent tasks).

Key endpoints
- `GET /api/background-tasks/{taskId}` - get status
- `GET /api/background-tasks` - list tasks (paged)
- `POST /api/background-tasks/{taskId}/cancel` - request cancel

Notes
- Tasks returned by imports/backups expose `taskId` which can be polled via this API.