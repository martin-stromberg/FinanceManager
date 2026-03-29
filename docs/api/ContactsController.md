# ContactsController

Path: `FinanceManager.Web.Controllers.ContactsController`

Purpose:
- Manage contacts (people, organizations, bank contacts) used across postings and drafts.

Key endpoints
- `GET /api/contacts` - list / search
- `GET /api/contacts/{id}` - get contact
- `POST /api/contacts` - create
- `PUT /api/contacts/{id}` - update
- `DELETE /api/contacts/{id}` - delete

Notes
- Special contact `Self` exists and is used for own account matching; only one self per user.
- Alias names used for automated classification are managed separately via AliasName endpoints.