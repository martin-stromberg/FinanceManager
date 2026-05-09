# StatementDraftEntriesController

Pfad: `FinanceManager.Web.Controllers.StatementDraftEntriesController`

API für QuickEdit-Batch-Updates von Draft-Entries.

Verwandter Endpunkt:
- [StatementDraftsController](./StatementDraftsController.md)

---

## Endpunkt: Draft-Entries im Batch aktualisieren

### 1) Übersicht
Aktualisiert mehrere Einträge eines Statement-Drafts in einem Request. Bei Validierungsfehlern wird ein strukturierter Fehler pro Entry/Feld zurückgegeben.

### 2) HTTP-Methode & Pfad
`POST /api/statement-drafts/{draftId}/entries/batch-update`

### 3) Authentifizierung
Bearer Token erforderlich.

### 4) Request
**Header**
- `Authorization: Bearer <JWT>`
- `Content-Type: application/json`

**Path-Parameter**
- `draftId` (`uuid`, required) – ID des Statement-Drafts.

**Request-Body**
- `updates` (`array`, required)
  - `entryId` (`uuid`, required)
  - `fields` (`object`, required) – Key/Value-Feldänderungen.

Vollständiges Beispiel:
```json
{
  "updates": [
    {
      "entryId": "91c8184d-0d2d-4b86-9d95-96d6ddcf31f5",
      "fields": {
        "BookingDate": "2026-03-01",
        "ValutaDate": "2026-03-02",
        "Amount": 125.4,
        "Subject": "Miete März",
        "BookingDescription": "SEPA Lastschrift",
        "RecipientName": "Beispiel Vermieter",
        "Status": "Accounted"
      }
    },
    {
      "entryId": "16f966ca-ff0e-4fd3-9766-20f0712f7fdb",
      "fields": {
        "Amount": -39.99,
        "Subject": "Stromabschlag"
      }
    }
  ]
}
```

### 5) Response
**Erfolg (200 OK)**
```json
{
  "success": true,
  "updatedDraft": {
    "draftId": "62a4f3af-d857-4df1-8f56-e7b2c44571a9",
    "originalFileName": "kontoauszug.csv",
    "description": "Import März 2026",
    "detectedAccountId": "902133cc-fa31-4a29-95ab-08e2e2ef6877",
    "status": "Open",
    "totalAmount": 85.41,
    "isSplitDraft": false,
    "parentDraftId": null,
    "parentEntryId": null,
    "parentEntryAmount": null,
    "uploadGroupId": "1d33c0fd-b850-4a31-84f3-1e9e57c77d58",
    "entries": [],
    "prevInUpload": null,
    "nextInUpload": null
  }
}
```

**Fehlerfälle**
- `400 Bad Request` (z. B. keine Updates oder Feldvalidierung)
```json
{
  "success": false,
  "errors": [
    {
      "entryId": "91c8184d-0d2d-4b86-9d95-96d6ddcf31f5",
      "fieldErrors": [
        {
          "field": "BookingDate",
          "message": "Invalid date format"
        }
      ]
    }
  ]
}
```
- `401 Unauthorized`
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication required."
}
```
- `404 Not Found` (Route/Resource nicht gefunden)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Draft not found."
}
```
- `500 Internal Server Error`
```json
{
  "message": "Internal server error"
}
```

Hinweis: Bei fehlender Berechtigung auf den Draft kann `403 Forbidden` zurückkommen.

### 6) Beispiel (`curl`)
```bash
curl -X POST "https://your-domain/api/statement-drafts/62a4f3af-d857-4df1-8f56-e7b2c44571a9/entries/batch-update" \
  -H "Authorization: Bearer <JWT>" \
  -H "Content-Type: application/json" \
  -d "{\"updates\":[{\"entryId\":\"91c8184d-0d2d-4b86-9d95-96d6ddcf31f5\",\"fields\":{\"BookingDate\":\"2026-03-01\",\"Amount\":125.4,\"Subject\":\"Miete März\"}}]}"
```

Beispiel-Response:
```json
{
  "success": true,
  "updatedDraft": {
    "draftId": "62a4f3af-d857-4df1-8f56-e7b2c44571a9",
    "originalFileName": "kontoauszug.csv",
    "description": "Import März 2026",
    "detectedAccountId": "902133cc-fa31-4a29-95ab-08e2e2ef6877",
    "status": "Open",
    "totalAmount": 85.41,
    "isSplitDraft": false,
    "parentDraftId": null,
    "parentEntryId": null,
    "parentEntryAmount": null,
    "uploadGroupId": "1d33c0fd-b850-4a31-84f3-1e9e57c77d58",
    "entries": [],
    "prevInUpload": null,
    "nextInUpload": null
  }
}
```
