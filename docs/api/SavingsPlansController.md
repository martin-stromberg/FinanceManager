# SavingsPlansController

Path: `FinanceManager.Web.Controllers.SavingsPlansController`

Purpose:
- Manage savings plans including recurring plans and targets.

Key endpoints
- `GET /api/savings-plans` - list
- `GET /api/savings-plans/{id}` - get
- `POST /api/savings-plans` - create
- `PUT /api/savings-plans/{id}` - update
- `DELETE /api/savings-plans/{id}` - delete

Notes
- Plans have target amount/date and interval; assignment to entries is validated during booking.