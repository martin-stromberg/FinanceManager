# StatementDraftsController

Path: `FinanceManager.Web.Controllers.StatementDraftsController`

Purpose:
- Upload and manage statement drafts, classification, validation, booking, attachments, split/assign operations.

Key endpoints (summary):
- `POST /api/statement-drafts/upload` - Upload statement file to create draft(s)
- `GET /api/statement-drafts` - list drafts
- `GET /api/statement-drafts/{id}` - get draft with entries
- `GET /api/statement-drafts/{draftId}/entries/{entryId}` - get entry detail
- `POST /api/statement-drafts/{draftId}/entries` - add entry
- `PUT /api/statement-drafts/{draftId}/entries/{entryId}` - update entry core
- `POST /api/statement-drafts/{draftId}/entries/{entryId}/save-all` - save all advanced fields (savings/security)
- `POST /api/statement-drafts/{draftId}/book` - book draft or entry
- `POST /api/statement-drafts/{draftId}/validate` - validate draft or entry
- `POST /api/statement-drafts/{draftId}/set-account` - set detected account for draft

Notes:
- Validation codes include `SECURITY_ACCOUNT_NOT_ALLOWED`, `SECURITY_INVALID_CONTACT`, `SAVINGSPLAN_*`.
- Booking flow is partial-aware and respects account settings like `SecurityProcessingEnabled`.
