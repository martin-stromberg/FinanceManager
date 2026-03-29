# UsersController

Path: `FinanceManager.Web.Controllers.UsersController`

Purpose:
- Manage user profiles, existence checks, and admin user operations.

Key endpoints
- `GET /api/users/exists` - check if any user exists (public)
- `GET /api/users/me` - get current user profile
- `PUT /api/users/me` - update profile (display name, preferred language)
- `GET /api/users` - admin list users
- `POST /api/users` - admin create user

Notes
- Sensitive operations require Admin role. `GET /api/users/exists` is public to allow first-time setup logic.