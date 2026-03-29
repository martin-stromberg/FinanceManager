# UserSettingsController

Path: `FinanceManager.Web.Controllers.UserSettingsController`

Purpose:
- Manage per-user settings such as import split mode, notification preferences and localization.

Key endpoints
- `GET /api/user-settings` - get current user settings
- `PUT /api/user-settings` - update settings

Notes
- Settings include `ImportSplitMode`, `ImportMaxEntriesPerDraft`, `PreferredLanguage` etc.