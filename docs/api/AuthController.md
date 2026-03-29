# AuthController

Path: `FinanceManager.Web.Controllers.AuthController`

Purpose:
- Authentication endpoints: login, register, logout, token refresh (if applicable).

Key endpoints
- `POST /api/auth/login` - authenticate user; returns cookie with JWT and user info
- `POST /api/auth/register` - register new user
- `POST /api/auth/logout` - logout, clear auth cookie

Example login request
```json
POST /api/auth/login
{
  "username": "alice",
  "password": "s3cr3t"
}
```

Example response 200
```json
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "userName": "alice",
  "roles": ["User"]
}
```

Notes
- Login endpoint sets HttpOnly cookie `FinanceManager.Auth` containing the JWT.
- Refreshing is handled by middleware which issues refreshed cookies and headers when tokens approach expiry.