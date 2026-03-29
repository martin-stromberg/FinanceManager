# PostingsController

Path: `FinanceManager.Web.Controllers.PostingsController`

Purpose:
- Access and query postings (bank/contact/savings/security) and related aggregates.

Common endpoints:
- `GET /api/postings` - query postings with filters (account, contact, period, kind)
- `GET /api/postings/{id}` - get posting detail
- `POST /api/postings/export` - export postings for selected accounts/date range

Notes:
- Document aggregate semantics (Period, DateKind).
- Include examples for filters and paging.
