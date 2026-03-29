# BackupsController

Path: `FinanceManager.Web.Controllers.BackupsController`

Purpose:
- Trigger database backups, list backup records, restore from backup.

Key endpoints
- `POST /api/backups` - create backup (admin or owner)
- `GET /api/backups` - list backups for user
- `POST /api/backups/restore/{backupId}` - restore a backup (restricted)

Notes
- Backup/restore operations may be long-running and return a background task id.
- Restores should be used with caution and require appropriate permissions.