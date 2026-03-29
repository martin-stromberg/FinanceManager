# AttachmentsController

Path: `FinanceManager.Web.Controllers.AttachmentsController`

Purpose:
- Upload, download, list and manage file attachments used for accounts, postings, statements and symbols.

Key endpoints
- `POST /api/attachments` - upload (multipart/form-data) with parent kind and parent id
- `GET /api/attachments/{id}` - download attachment
- `GET /api/attachments?ownerUserId=...` - list attachments (filters)
- `DELETE /api/attachments/{id}` - delete attachment

Example upload (curl)
```
curl -X POST "/api/attachments" -H "Authorization: Bearer <jwt>" \
  -F "file=@logo.png" \
  -F "ownerUserId=00000000-0000-0000-0000-000000000000" \
  -F "entityKind=StatementDraft" \
  -F "entityId=550e8400-e29b-41d4-a716-446655440000"
```

Example response 201
```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "fileName": "logo.png",
  "contentType": "image/png",
  "sizeBytes": 10240,
  "uploadedUtc": "2024-01-01T12:00:00Z"
}
```

Notes
- Attachments are subject to owner scoping and authorization.
- Attachments may be reassigned programmatically (used when moving draft attachments to postings on booking).