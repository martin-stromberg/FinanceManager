# ContactsController

Path: `FinanceManager.Web/Controllers/ContactsController.cs`

## Purpose

Manages contacts for the current user and supports inline "create and assign" workflows from lookup contexts.

## Core endpoints

- `GET /api/contacts`
- `GET /api/contacts/{id}`
- `POST /api/contacts`
- `PUT /api/contacts/{id}`
- `DELETE /api/contacts/{id}`
- `POST /api/contacts/{id}/aliases`
- `DELETE /api/contacts/{id}/aliases/{aliasId}`
- `POST /api/contacts/{id}/merge`

## `POST /api/contacts` with parent assignment

### Request

`ContactCreateRequest` supports optional parent context:

```json
{
  "name": "Inline Contact",
  "type": "Other",
  "categoryId": null,
  "description": "Created from statement entry context",
  "isPaymentIntermediary": false,
  "parent": {
    "parentKind": "statement-drafts/entries",
    "parentId": "00000000-0000-0000-0000-000000000000",
    "field": "ContactId"
  }
}
```

### Success behavior

1. Contact is created via `IContactService.CreateAsync`.
2. If `parent` is present, controller calls `IParentAssignmentService.TryAssignAsync(..., createdKind: "contacts", ...)`.
3. On success, API returns `201 Created`.

### Error contract (409 Conflict)

If parent assignment fails, API returns `409 Conflict` with:

```json
{
  "code": "Err_Conflict_ParentAssignment",
  "message": "Localized message or fallback text"
}
```

Localization key:
- `API_Contacts_Err_Conflict_ParentAssignment`

### Rollback behavior

When assignment fails, controller attempts to delete the newly created contact (`DeleteAsync`) before returning `409`.
- **Intended outcome:** no orphaned created contact remains.
- **If rollback delete fails:** API still returns `409`, and warning logs include `RollbackSucceeded=false`.

### Idempotency behavior

- `POST /api/contacts` is **not** idempotent as an endpoint (repeating the request creates additional contacts).
- Parent assignment for the same `(EntryId, ContactId)` pair is idempotent in `ParentAssignmentService`: repeated assignment is a no-op and returns success.

## Related feature artifacts

- Requirements: [`../requirements/statement-contact-auto-assignment-requirements.md`](../requirements/statement-contact-auto-assignment-requirements.md)
- Planning: [`../planning/planning-statement-contact-auto-assignment.md`](../planning/planning-statement-contact-auto-assignment.md)
- Architecture: [`../architecture/architecture-blueprint-statement-contact-auto-assignment.md`](../architecture/architecture-blueprint-statement-contact-auto-assignment.md)
- Coverage/Test plan:
  - [`../tests/phase2-contact-parent-assignment-coverage-gaps.md`](../tests/phase2-contact-parent-assignment-coverage-gaps.md)
  - [`../tests/phase2-contact-parent-assignment-test-plan.md`](../tests/phase2-contact-parent-assignment-test-plan.md)