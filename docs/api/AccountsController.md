# AccountsController

Path: `FinanceManager.Web.Controllers.AccountsController`

Purpose:
- Manage user bank accounts: create, update, list, get, delete, symbol attachment.

Common endpoints (fill details):
- `GET /api/accounts` - list accounts (paged)
- `GET /api/accounts/{id}` - get account details
- `POST /api/accounts` - create account
- `PUT /api/accounts/{id}` - update account
- `DELETE /api/accounts/{id}` - delete account
- `POST /api/accounts/{id}/symbol` - set symbol attachment

Notes / TODO:
- Add request/response DTOs examples
- Document authorization scopes and validation rules (e.g. IBAN rules, SecurityProcessingEnabled behavior)
